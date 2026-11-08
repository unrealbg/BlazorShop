#!/usr/bin/env bash
# Maps the output of collect-version-evidence.sh to the commits it is consistent with.
#
#   bash scripts/vps-evidence/match-version-evidence.sh [--all-ancestors] <evidence-file> [ref]
#
# Local only: it reads the evidence file and git objects of a full (non-shallow) clone.
#
# Per service it lists the commits whose build inputs match the running image:
# - api: /app/appsettings.json and the complete database migration list (IDs, count and head);
# - storefront: /app/appsettings.json;
# - web: the nginx files at the container paths the Web Dockerfile *of that commit* copies to,
#   so older images that copied nginx.conf to conf.d/default.conf are compared correctly.
# A commit is a deployment candidate only when all three services match it. Matching files and
# migrations do not prove identical binaries: the result is a candidate set for the owner to
# confirm, and services built from different commits show up as an empty intersection.
#
# Exit status: 0 candidates found, 1 no consistent commit, 2 invalid evidence, usage or history.
# Bash 4.4 or later is required: inherit_errexit and expanding empty arrays under `set -u` are 4.4
# features. The check runs first and uses only syntax that older bash and POSIX sh also parse.
case "${BASH_VERSION-}" in
    4.[4-9]* | [5-9].* | [1-9][0-9].*) ;;
    *)
        echo "match-version-evidence.sh: bash 4.4 or later is required (found ${BASH_VERSION:-a shell that is not bash})" >&2
        exit 2
        ;;
esac
set -Eeuo pipefail
shopt -s inherit_errexit

readonly SCHEMA="blazorshop-vps-version-evidence/v2"
readonly MIGRATIONS_DIR="BlazorShop.Infrastructure/Migrations"
readonly API_APPSETTINGS="BlazorShop.Presentation/BlazorShop.API/appsettings.json"
readonly STOREFRONT_APPSETTINGS="BlazorShop.Presentation/BlazorShop.Storefront/appsettings.json"
readonly WEB_DOCKERFILE="BlazorShop.Presentation/BlazorShop.Web/Dockerfile"
readonly MIGRATION_PATTERN='^[0-9]{14}_[A-Za-z0-9]+$'

usage() {
    echo "Usage: $0 [--all-ancestors] <evidence-file> [ref, default origin/master]" >&2
    exit 2
}
invalid() {
    echo "invalid evidence: $*" >&2
    exit 2
}

HISTORY="first-parent"
if [[ "${1:-}" == "--all-ancestors" ]]; then
    HISTORY="all-ancestors"
    shift
fi
[[ $# -ge 1 && $# -le 2 ]] || usage
readonly EVIDENCE="$1"
readonly REF="${2:-origin/master}"
[[ -f "$EVIDENCE" ]] || usage

# ---- Strict parsing: key=value lines, a checksum over them, then evidence_complete=true. -------
declare -A VALUE=()
MIGRATIONS=()
BODY=()
CHECKSUM=""
state="body"
while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    case "$state" in
        body)
            if [[ "$line" == evidence_lines_sha256=* ]]; then
                CHECKSUM="${line#*=}"
                state="checksum"
                continue
            fi
            [[ "$line" =~ ^([A-Za-z0-9_.]+)=(.+)$ ]] || invalid "malformed line '${line:0:80}'"
            key="${BASH_REMATCH[1]}"
            value="${BASH_REMATCH[2]}"
            BODY+=("$line")
            if [[ "$key" == "migration" ]]; then
                MIGRATIONS+=("$value")
                continue
            fi
            [[ -z "${VALUE[$key]+set}" ]] || invalid "duplicate key '$key'"
            VALUE[$key]="$value"
            ;;
        checksum)
            [[ "$line" == "evidence_complete=true" ]] || invalid "the checksum line must be followed by evidence_complete=true"
            state="complete"
            ;;
        complete)
            [[ -z "$line" ]] || invalid "content after evidence_complete=true"
            ;;
    esac
done < "$EVIDENCE"
[[ "$state" == "complete" ]] || invalid "incomplete file: no checksum line or no evidence_complete=true"
[[ "$CHECKSUM" =~ ^[0-9a-f]{64}$ ]] || invalid "malformed evidence_lines_sha256"
[[ "$(printf '%s\n' "${BODY[@]}" | sha256sum | awk '{print $1}')" == "$CHECKSUM" ]] \
    || invalid "checksum mismatch: the file was truncated or edited"

[[ "${VALUE[schema]:-}" == "$SCHEMA" ]] || invalid "unsupported schema '${VALUE[schema]:-absent}'"
readonly HASH_KEYS=(api_appsettings storefront_appsettings web_nginx_template web_security_headers web_nginx_conf_d)
for key in "${HASH_KEYS[@]}"; do
    value="${VALUE[sha256.$key]:-}"
    [[ "$value" =~ ^[0-9a-f]{64}$ || "$value" == "missing" ]] || invalid "sha256.$key must be a SHA-256 or 'missing'"
done
[[ "${VALUE[migration_count]:-}" =~ ^[1-9][0-9]*$ ]] || invalid "migration_count must be a positive integer"
[[ "${VALUE[migration_head]:-}" =~ $MIGRATION_PATTERN ]] || invalid "migration_head is missing or malformed"
for migration in "${MIGRATIONS[@]}"; do
    [[ "$migration" =~ $MIGRATION_PATTERN ]] || invalid "malformed migration id '${migration:0:80}'"
done
[[ ${#MIGRATIONS[@]} -eq ${VALUE[migration_count]} ]] \
    || invalid "migration_count is ${VALUE[migration_count]} but ${#MIGRATIONS[@]} migration lines are present"
EVIDENCE_MIGRATIONS="$(printf '%s\n' "${MIGRATIONS[@]}" | LC_ALL=C sort)"
[[ "$(printf '%s\n' "${MIGRATIONS[@]}" | LC_ALL=C sort -u | wc -l | tr -d ' ')" -eq ${#MIGRATIONS[@]} ]] \
    || invalid "duplicate migration ids"
[[ "$(printf '%s\n' "$EVIDENCE_MIGRATIONS" | tail -n 1)" == "${VALUE[migration_head]}" ]] \
    || invalid "migration_head is not the newest migration in the list"
readonly EVIDENCE_MIGRATIONS

# ---- History preconditions. -------------------------------------------------------------------
git rev-parse --is-inside-work-tree > /dev/null 2>&1 || { echo "not inside a git repository" >&2; exit 2; }
[[ "$(git rev-parse --is-shallow-repository)" == "false" ]] \
    || { echo "shallow clone: history is incomplete; run 'git fetch --unshallow' first" >&2; exit 2; }
REF_SHA="$(git rev-parse --verify --quiet "$REF^{commit}")" || { echo "unknown ref '$REF'" >&2; exit 2; }
readonly REF_SHA

# ---- Expected build inputs per commit. --------------------------------------------------------
blob_at() {
    git rev-parse --quiet --verify "$1:$2" 2> /dev/null || true
}

# Model checkout of an LF text blob as CRLF. Read to EOF without stripping the last newline.
# A NUL delimiter or an existing CR rules out this conversion, including mixed line endings.
crlf_checkout() {
    local data
    if IFS= read -r -d '' data || [[ "$data" == *$'\r'* ]]; then
        return 1
    fi
    printf '%s' "${data//$'\n'/$'\r\n'}"
}

content_matches() {
    local actual="$3" blob expected
    blob="$(blob_at "$1" "$2")"
    if [[ -z "$blob" ]]; then
        [[ "$actual" == "missing" ]]
        return
    fi
    [[ "$actual" != "missing" ]] || return 1
    expected="$(git cat-file blob "$blob" | sha256sum | awk '{print $1}')" || return 1
    [[ "$actual" != "$expected" ]] || return 0
    expected="$(git cat-file blob "$blob" | crlf_checkout | sha256sum | awk '{print $1}')" || return 1
    [[ "$actual" == "$expected" ]]
}

# "dest<TAB>source" lines for plain `COPY <source> <dest>` instructions of the Web Dockerfile.
web_copies() {
    local blob
    blob="$(blob_at "$1" "$WEB_DOCKERFILE")"
    [[ -n "$blob" ]] || return 1
    git cat-file blob "$blob" | tr -d '\r' | awk '$1 == "COPY" && NF == 3 && $2 !~ /^--/ { print $3 "\t" $2 }'
}

# Match a Web container path against the source this Dockerfile copies there. An uncopied
# conf.d/default.conf may be rendered at runtime and its hash is intentionally unconstrained.
web_path_matches() {
    local commit="$1" copies="$2" container_path="$3" when_not_copied="$4" actual="$5" source
    source="$(printf '%s\n' "$copies" | awk -F '\t' -v dest="$container_path" '$1 == dest { print $2; exit }')"
    if [[ -z "$source" ]]; then
        [[ "$when_not_copied" == "any" || "$actual" == "$when_not_copied" ]]
        return
    fi
    content_matches "$commit" "$source" "$actual"
}

api_matches() {
    local commit="$1" migrations
    content_matches "$commit" "$API_APPSETTINGS" "${VALUE[sha256.api_appsettings]}" || return 1
    migrations="$(git ls-tree --name-only "$commit" "$MIGRATIONS_DIR/" \
        | sed -n -E 's|^.*/([0-9]{14}_[A-Za-z0-9]+)\.cs$|\1|p' | LC_ALL=C sort)"
    [[ "$migrations" == "$EVIDENCE_MIGRATIONS" ]]
}

storefront_matches() {
    content_matches "$1" "$STOREFRONT_APPSETTINGS" "${VALUE[sha256.storefront_appsettings]}"
}

web_matches() {
    local commit="$1" copies
    copies="$(web_copies "$commit")" || return 1
    web_path_matches "$commit" "$copies" /etc/nginx/templates/default.conf.template missing "${VALUE[sha256.web_nginx_template]}" || return 1
    web_path_matches "$commit" "$copies" /etc/nginx/snippets/blazorshop-security-headers.conf missing "${VALUE[sha256.web_security_headers]}" || return 1
    web_path_matches "$commit" "$copies" /etc/nginx/conf.d/default.conf any "${VALUE[sha256.web_nginx_conf_d]}"
}

# ---- Scan. ------------------------------------------------------------------------------------
if [[ "$HISTORY" == "first-parent" ]]; then
    REV_LIST=(git rev-list --first-parent "$REF_SHA")
else
    REV_LIST=(git rev-list "$REF_SHA")
fi

echo "evidence_schema=${VALUE[schema]}"
echo "evidence_migration_count=${VALUE[migration_count]}"
echo "evidence_migration_head=${VALUE[migration_head]}"
echo "history=$HISTORY ref=$REF_SHA"

declare -A COUNT=([api]=0 [storefront]=0 [web]=0 [all]=0)
while read -r commit; do
    ok_all=1
    for service in api storefront web; do
        if "${service}_matches" "$commit"; then
            COUNT[$service]=$((COUNT[$service] + 1))
            echo "candidate.$service=$commit"
        else
            ok_all=0
        fi
    done
    if [[ $ok_all -eq 1 ]]; then
        COUNT[all]=$((COUNT[all] + 1))
        git log -1 --format='candidate=%H %cs %s' "$commit"
    fi
done < <("${REV_LIST[@]}")

for service in api storefront web; do
    echo "candidate_count.$service=${COUNT[$service]}"
done
echo "candidate_count=${COUNT[all]}"

if [[ ${COUNT[all]} -eq 0 ]]; then
    if [[ ${COUNT[api]} -gt 0 && ${COUNT[storefront]} -gt 0 && ${COUNT[web]} -gt 0 ]]; then
        echo "No single commit matches all services: the images were probably built from different commits or checkout states." >&2
    else
        echo "No commit in the scanned history matches every service; consider --all-ancestors, another ref, or a modified checkout." >&2
    fi
    exit 1
fi
