#!/usr/bin/env bash
# Maps the output of collect-version-evidence.sh to the master commits it is consistent with.
#
#   bash scripts/vps-evidence/match-version-evidence.sh vps-evidence.txt [ref]
#
# A commit is a candidate only if, at that commit, every reported file hash matches the
# repository file that is copied verbatim into the image, and the newest migration in the tree
# equals the database migration head. Local only: it reads git objects and the evidence file.
# The result is a candidate set for the owner to confirm, not proof of a single build.
set -Eeuo pipefail

if [[ $# -lt 1 || $# -gt 2 ]]; then
    echo "Usage: $0 <evidence-file> [ref, default origin/master]" >&2
    exit 2
fi
readonly EVIDENCE="$1"
readonly REF="${2:-origin/master}"
readonly MIGRATIONS_DIR="BlazorShop.Infrastructure/Migrations"

declare -A FILE_PATHS=(
    [api_appsettings]="BlazorShop.Presentation/BlazorShop.API/appsettings.json"
    [storefront_appsettings]="BlazorShop.Presentation/BlazorShop.Storefront/appsettings.json"
    [web_nginx_template]="BlazorShop.Presentation/BlazorShop.Web/nginx.conf"
    [web_security_headers]="BlazorShop.Presentation/BlazorShop.Web/nginx-security-headers.conf"
)

evidence_value() {
    sed -n "s/^$1=//p" "$EVIDENCE" | tr -d '\r' | head -n 1
}

declare -A EXPECTED=()
for key in "${!FILE_PATHS[@]}"; do
    value="$(evidence_value "sha256.$key")"
    # "missing" (file absent in the running image) is evidence too: the commit must not have it.
    if [[ "$value" =~ ^[0-9a-f]{64}$ || "$value" == "missing" ]]; then
        EXPECTED[$key]="$value"
    else
        echo "note: no usable sha256.$key in the evidence (${value:-absent}); not used for matching" >&2
    fi
done
MIGRATION_HEAD="$(evidence_value migration_head)"
readonly MIGRATION_HEAD
if [[ ! "$MIGRATION_HEAD" =~ ^[0-9]{14}_[A-Za-z0-9]+$ ]]; then
    echo "The evidence has no valid migration_head." >&2
    exit 1
fi
if [[ ${#EXPECTED[@]} -eq 0 ]]; then
    echo "The evidence has no usable file hashes." >&2
    exit 1
fi

# SHA-256 of each blob is computed once.
declare -A BLOB_SHA256=()
blob_sha256() {
    local blob="$1"
    if [[ -z "${BLOB_SHA256[$blob]:-}" ]]; then
        BLOB_SHA256[$blob]="$(git cat-file blob "$blob" | sha256sum | awk '{print $1}')"
    fi
    printf '%s' "${BLOB_SHA256[$blob]}"
}

echo "evidence_migration_head=$MIGRATION_HEAD"
for key in "${!EXPECTED[@]}"; do
    echo "evidence_sha256.$key=${EXPECTED[$key]}"
done

candidates=0
while read -r commit; do
    head_at_commit="$(git ls-tree --name-only "$commit" "$MIGRATIONS_DIR/" \
        | sed -n -E 's|.*/([0-9]{14}_[A-Za-z0-9]+)\.cs$|\1|p' | grep -v -E '\.Designer$' | LC_ALL=C sort | tail -n 1 || true)"
    [[ "$head_at_commit" == "$MIGRATION_HEAD" ]] || continue
    match=1
    for key in "${!EXPECTED[@]}"; do
        blob="$(git rev-parse --quiet --verify "$commit:${FILE_PATHS[$key]}" 2>/dev/null || true)"
        if [[ "${EXPECTED[$key]}" == "missing" ]]; then
            [[ -z "$blob" ]] || { match=0; break; }
        elif [[ -z "$blob" || "$(blob_sha256 "$blob")" != "${EXPECTED[$key]}" ]]; then
            match=0
            break
        fi
    done
    if [[ "$match" -eq 1 ]]; then
        candidates=$((candidates + 1))
        git log -1 --format='candidate=%H %cs %s' "$commit"
    fi
done < <(git rev-list --first-parent "$REF")

echo "candidate_count=$candidates"
if [[ "$candidates" -eq 0 ]]; then
    echo "No commit on $REF matches every reported hash and the migration head; the deployment may be from another branch or a modified checkout." >&2
    exit 1
fi
