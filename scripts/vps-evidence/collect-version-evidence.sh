#!/usr/bin/env bash
# Read-only evidence of the BlazorShop version that is actually running on a Docker Compose host.
#
# Run it from a reviewed local copy over SSH; nothing has to be installed or updated on the host:
#   ssh <host> 'bash -s -- <compose-project> [deployment-checkout-dir]' \
#       < scripts/vps-evidence/collect-version-evidence.sh > vps-evidence.txt
# Without arguments it lists the running Compose projects and exits 2.
# --inventory-only <project> <db-name> <read-only-db-user> skips checkout inspection,
# volume traversal and database size/table statistics.
#
# Read-only by construction: it runs only `docker ps`, `docker inspect`, `docker image inspect`,
# and `docker exec` of sh/sha256sum/find/printf/psql in already running containers, plus `git` read
# commands without optional locks. PostgreSQL queries run in read-only transactions and never
# prompt for a password. It never runs `docker compose`, so the Compose environment is not rendered,
# and it prints no environment variables, connection strings, keys, file contents or row data.
#
# All-or-nothing output: evidence is printed only after every item was collected and validated,
# followed by a checksum line and `evidence_complete=true`. Any failure prints a diagnostic on
# stderr, nothing on stdout, and exits non-zero, so a partial file cannot be mistaken for evidence.
# Bash 4.4 or later is required: inherit_errexit and expanding empty arrays under `set -u` are 4.4
# features. The check runs first and uses only syntax that older bash and POSIX sh also parse.
case "${BASH_VERSION-}" in
    4.[4-9]* | [5-9].* | [1-9][0-9].*) ;;
    *)
        echo "evidence collection failed: bash 4.4 or later is required (found ${BASH_VERSION:-a shell that is not bash})" >&2
        exit 1
        ;;
esac
set -Eeuo pipefail
shopt -s inherit_errexit

readonly SCHEMA="blazorshop-vps-version-evidence/v2"
readonly SERVICES=(postgres api storefront web)
# Exit status of the in-container probe when a path provably does not exist.
readonly ABSENT_STATUS=3

fail() {
    echo "evidence collection failed: $*" >&2
    exit 1
}
# Any unexpected failure exits 1 (2 is reserved for usage), whatever the failing command returned.
trap 'echo "evidence collection failed at line $LINENO: $BASH_COMMAND" >&2; exit 1' ERR

if [[ $# -lt 1 ]]; then
    echo "Usage: bash -s -- <compose-project> [deployment-checkout-dir] < collect-version-evidence.sh" >&2
    echo "Running Compose projects:" >&2
    docker ps --filter status=running --format '{{.Label "com.docker.compose.project"}}' | sed '/^$/d' | sort -u | sed 's/^/  /' >&2
    exit 2
fi
if [[ "$1" == --inventory-only ]]; then
    [[ $# -eq 4 ]] || fail "expected --inventory-only <project> <db-name> <read-only-db-user>"
    readonly INVENTORY_ONLY=true PROJECT="$2" DB_NAME="$3" DB_USER="$4" CHECKOUT_DIR=""
else
    [[ $# -le 2 ]] || fail "expected at most two arguments"
    readonly INVENTORY_ONLY=false PROJECT="$1" CHECKOUT_DIR="${2:-}"
    readonly DB_NAME="${BLAZORSHOP_EVIDENCE_DB_NAME:-blazorshop}"
    readonly DB_USER="${BLAZORSHOP_EVIDENCE_DB_USER:-blazorshop}"
fi
[[ "$PROJECT" =~ ^[a-z0-9][a-z0-9_-]*$ ]] || fail "invalid Compose project name '$PROJECT'"
[[ "$DB_NAME" =~ ^[A-Za-z0-9_]+$ && "$DB_USER" =~ ^[A-Za-z0-9_]+$ ]] || fail "invalid database name or user"

# Every value must be non-empty, so a helper that failed inside a command substitution (and
# therefore printed nothing) can never contribute a silently empty evidence line.
OUTPUT=()
emit() {
    local key="$1"
    local value="$2"
    [[ "$key" =~ ^[A-Za-z0-9_.]+$ ]] || fail "internal: invalid key '$key'"
    [[ "$value" != *$'\n'* && "$value" != *$'\r'* && -n "$value" ]] || fail "empty or multi-line value for $key"
    OUTPUT+=("$key=$value")
}

# The single running service container of this project (one-off `compose run` containers excluded).
container_for() {
    local service="$1"
    local listed
    listed="$(docker ps --quiet --no-trunc \
        --filter "label=com.docker.compose.project=$PROJECT" \
        --filter "label=com.docker.compose.service=$service" \
        --filter "label=com.docker.compose.oneoff=False" \
        --filter status=running)" || fail "docker ps failed for service '$service'"
    local ids=()
    [[ -z "$listed" ]] || mapfile -t ids <<< "$listed"
    if [[ ${#ids[@]} -eq 1 ]]; then
        printf '%s' "${ids[0]}"
        return 0
    fi
    if [[ ${#ids[@]} -eq 0 ]]; then
        local others
        others="$(docker ps --all --quiet \
            --filter "label=com.docker.compose.project=$PROJECT" \
            --filter "label=com.docker.compose.service=$service" | wc -l | tr -d ' ')"
        fail "no running '$service' container in Compose project '$PROJECT' ($others non-running)"
    fi
    fail "${#ids[@]} running '$service' containers in Compose project '$PROJECT'; expected exactly one"
}

inspect_field() {
    local value
    value="$(docker inspect --format "$2" "$1")"
    [[ -n "$value" ]] || fail "empty docker inspect field $2"
    printf '%s' "$value"
}

# POSIX sh prelude of the in-container probes (the images are not guaranteed to have bash). It exits
# 3 only when "$1" provably does not exist: neither it nor a dangling link is there, and its nearest
# existing ancestor is a searchable directory. A path below an unsearchable directory also fails
# `[ -e ]`, so that case exits 5 (cannot tell) instead of being reported as absent.
# shellcheck disable=SC2016 # the probe is sh code expanded inside the container, not here
readonly ABSENCE_PROBE='
    case $1 in /*) ;; *) echo "not an absolute path: $1" >&2; exit 4 ;; esac
    if [ ! -e "$1" ] && [ ! -L "$1" ]; then
        p=$1
        while [ ! -e "$p" ] && [ ! -L "$p" ]; do p=${p%/*}; p=${p:-/}; done
        if [ -d "$p" ] && [ -x "$p" ]; then exit 3; fi
        echo "cannot tell whether $1 exists: $p is not a searchable directory" >&2
        exit 5
    fi'

# SHA-256 of a regular file inside a running container, or "missing" only when the path provably
# does not exist. Any other failure (docker exec, permissions, not a regular file) aborts.
file_sha256() {
    local container="$1"
    local path="$2"
    local output status=0 restriction=""
    if "$INVENTORY_ONLY"; then
        # Reject symlinks in the fixed path or its parents.
        # shellcheck disable=SC2016
        restriction='
        canonical=$(readlink -f "$1") || exit 5
        [ "$canonical" = "$1" ] || exit 5'
    fi
    # `|| status=$?` keeps the expected "absent" exit from tripping errexit and the ERR trap.
    output="$(docker exec "$container" sh -c "$ABSENCE_PROBE$restriction"'
        if [ ! -f "$1" ]; then echo "not a regular file: $1" >&2; exit 4; fi
        sha256sum "$1"' sh "$path")" || status=$?
    if [[ $status -eq $ABSENT_STATUS ]]; then
        printf 'missing'
        return 0
    fi
    [[ $status -eq 0 ]] || fail "could not hash $path in container ${container:0:12} (exit $status)"
    local hash="${output%% *}"
    [[ "$hash" =~ ^[0-9a-f]{64}$ ]] || fail "unexpected sha256sum output for $path"
    printf '%s' "$hash"
}

# Number of regular files below a directory, or "absent" when the directory provably does not exist.
# The probe runs under plain sh, which has no pipefail: `find | wc -l` would turn a failed or
# partial traversal into a successful smaller count. Instead find prints one "." per file into a
# command substitution, whose exit status is find's own, and the count is the length of that string
# (also correct for file names that contain newlines).
file_count() {
    local output status=0
    output="$(docker exec "$1" sh -c "$ABSENCE_PROBE"'
        if [ ! -d "$1" ]; then echo "not a directory: $1" >&2; exit 4; fi
        marks=$(find "$1" -type f -exec printf ".%.0s" {} +) || exit 5
        echo "${#marks}"' sh "$2")" || status=$?
    if [[ $status -eq $ABSENT_STATUS ]]; then
        printf 'absent'
        return 0
    fi
    [[ $status -eq 0 ]] || fail "could not count files in $2 (exit $status)"
    output="$(printf '%s' "$output" | tr -d ' ')"
    [[ "$output" =~ ^[0-9]+$ ]] || fail "unexpected file count for $2"
    printf '%s' "$output"
}

readonly_psql() {
    docker exec --env PGOPTIONS='-c default_transaction_read_only=on' "$1" \
        psql -X -w --set ON_ERROR_STOP=1 --tuples-only --no-align -U "$DB_USER" -d "$DB_NAME" --command "$2"
}

inventory_metadata() {
    local service="$1" id="$2" image_id="$3" platform revision digests digest mounts type name destination
    local digest_count=0 mount_count=0
    local -A seen_destinations=()
    platform="$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$image_id")"
    [[ "$platform" =~ ^[a-z0-9_]+/[a-z0-9_]+$ ]] || fail "invalid image platform for $service"
    emit "${service}_image_platform" "$platform"
    revision="$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$image_id")"
    if [[ -z "$revision" || "$revision" == '<no value>' ]]; then
        revision=absent
    elif [[ ! "$revision" =~ ^([0-9a-fA-F]{40}|[0-9a-fA-F]{64})$ ]]; then
        revision=unrecognized
    fi
    emit "${service}_image_revision" "$revision"
    digests="$(docker image inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$image_id")"
    while IFS= read -r digest; do
        [[ -n "$digest" ]] || continue
        [[ "$digest" =~ ^[a-z0-9][a-z0-9./:_-]*@sha256:[0-9a-f]{64}$ ]] || fail "invalid RepoDigest for $service"
        digest_count=$((digest_count + 1))
        emit "${service}_repo_digest.$digest_count" "$digest"
    done <<< "$digests"
    emit "${service}_repo_digest_count" "$digest_count"

    mounts="$(docker inspect --format '{{range .Mounts}}{{printf "%s|%s|%s\n" .Type .Name .Destination}}{{end}}' "$id")"
    while IFS='|' read -r type name destination; do
        [[ -n "$type" ]] || continue
        [[ "$type" == volume && "$name" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*$ ]] || fail "unexpected mount type or volume name for $service"
        case "$service:$destination" in
            postgres:/var/lib/postgresql/data | api:/app/uploads | api:/root/.aspnet/DataProtection-Keys) ;;
            *) fail "unexpected persistent mount destination for $service" ;;
        esac
        [[ -z "${seen_destinations[$destination]+set}" ]] || fail "duplicate mount destination for $service"
        seen_destinations[$destination]=1
        mount_count=$((mount_count + 1))
        emit "${service}_volume.$mount_count.name" "$name"
        emit "${service}_volume.$mount_count.destination" "$destination"
    done <<< "$mounts"
    case "$service:$mount_count" in
        postgres:1 | api:2 | storefront:0 | web:0) ;;
        *) fail "missing expected persistent mount for $service" ;;
    esac
    emit "${service}_volume_count" "$mount_count"
}

inventory_history() {
    docker exec "$1" env -i PATH=/usr/local/bin:/usr/bin:/bin PGPASSFILE=/dev/null/r09 PGCONNECT_TIMEOUT=3 \
        psql -X -w --quiet --set ON_ERROR_STOP=1 --tuples-only --no-align \
        --host=/var/run/postgresql --port=5432 -U "$DB_USER" -d "$DB_NAME" --command '
            BEGIN ISOLATION LEVEL REPEATABLE READ READ ONLY;
            SET LOCAL statement_timeout = 5000;
            SET LOCAL lock_timeout = 1000;
            SELECT current_setting('\''server_version_num'\'');
            SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C";
            ROLLBACK;' 2>/dev/null
}

emit schema "$SCHEMA"
emit collected_at_utc "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
emit compose_project "$PROJECT"

# The checkout is context only: images may have been built from another state of it.
if [[ -n "$CHECKOUT_DIR" ]]; then
    git -C "$CHECKOUT_DIR" rev-parse --is-inside-work-tree > /dev/null 2>&1 \
        || fail "'$CHECKOUT_DIR' is not a git working tree"
    export GIT_OPTIONAL_LOCKS=0
    head="$(git -C "$CHECKOUT_DIR" rev-parse --verify HEAD)"
    [[ "$head" =~ ^[0-9a-f]{40}$ ]] || fail "unexpected checkout HEAD"
    emit checkout_head "$head"
    emit checkout_head_commit_time "$(git -C "$CHECKOUT_DIR" log -1 --format=%cI HEAD)"
    emit checkout_branch "$(git -C "$CHECKOUT_DIR" rev-parse --abbrev-ref HEAD)"
    modified="$(git -C "$CHECKOUT_DIR" status --porcelain --untracked-files=no)"
    emit checkout_modified_files "$(printf '%s' "$modified" | sed '/^$/d' | wc -l | tr -d ' ')"
else
    emit checkout_head not-collected
fi

declare -A CONTAINER=() STARTED_AT=()
for service in "${SERVICES[@]}"; do
    CONTAINER[$service]="$(container_for "$service")"
done

for service in "${SERVICES[@]}"; do
    id="${CONTAINER[$service]}"
    image_id="$(inspect_field "$id" '{{.Image}}')"
    STARTED_AT[$service]="$(inspect_field "$id" '{{.State.StartedAt}}')"
    state="$(inspect_field "$id" '{{.State.Status}}')"
    if "$INVENTORY_ONLY"; then
        [[ "$state" == running && "$id" =~ ^[0-9a-f]{64}$ && "$image_id" =~ ^sha256:[0-9a-f]{64}$ ]] \
            || fail "invalid running container or image ID for $service"
    fi
    emit "${service}_container_id" "$id"
    emit "${service}_state" "$state"
    emit "${service}_started_at" "${STARTED_AT[$service]}"
    if ! "$INVENTORY_ONLY"; then
        emit "${service}_image_ref" "$(inspect_field "$id" '{{.Config.Image}}')"
    fi
    emit "${service}_image_id" "$image_id"
    image_created="$(docker image inspect --format '{{.Created}}' "$image_id")"
    emit "${service}_image_created" "$image_created"
    if "$INVENTORY_ONLY"; then
        inventory_metadata "$service" "$id" "$image_id"
    fi
done
if ! "$INVENTORY_ONLY"; then
    emit compose_working_dir "$(inspect_field "${CONTAINER[api]}" '{{index .Config.Labels "com.docker.compose.project.working_dir"}}')"
fi

# Files the Dockerfiles copy verbatim from the repository. conf.d/default.conf is the verbatim
# nginx.conf in older Web images and a runtime-rendered file in newer ones.
emit sha256.api_appsettings "$(file_sha256 "${CONTAINER[api]}" /app/appsettings.json)"
emit sha256.storefront_appsettings "$(file_sha256 "${CONTAINER[storefront]}" /app/appsettings.json)"
emit sha256.web_nginx_template "$(file_sha256 "${CONTAINER[web]}" /etc/nginx/templates/default.conf.template)"
emit sha256.web_security_headers "$(file_sha256 "${CONTAINER[web]}" /etc/nginx/snippets/blazorshop-security-headers.conf)"
emit sha256.web_nginx_conf_d "$(file_sha256 "${CONTAINER[web]}" /etc/nginx/conf.d/default.conf)"

postgres="${CONTAINER[postgres]}"
migrations=()
if "$INVENTORY_ONLY"; then
    history="$(inventory_history "$postgres")" || fail "bounded migration query failed"
    mapfile -t history_lines <<< "$history"
    server_version="${history_lines[0]}"
    [[ "$server_version" =~ ^[1-9][0-9]{5,7}$ ]] || fail "unexpected PostgreSQL version number"
    emit postgres_server_version_num "$server_version"
    migrations=("${history_lines[@]:1}")
    migration_count="${#migrations[@]}"
    [[ "$migration_count" -gt 0 ]] || fail "no migration IDs returned"
    migration_head="${migrations[-1]}"
else
    emit uploads_files "$(file_count "${CONTAINER[api]}" /app/uploads)"
    emit data_protection_key_files "$(file_count "${CONTAINER[api]}" /root/.aspnet/DataProtection-Keys)"
    emit postgres_server_version "$(readonly_psql "$postgres" 'SHOW server_version;')"
    database_size="$(readonly_psql "$postgres" 'SELECT pg_database_size(current_database());')"
    [[ "$database_size" =~ ^[0-9]+$ ]] || fail "unexpected database size"
    emit database_size_bytes "$database_size"
    migration_count="$(readonly_psql "$postgres" 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
    migration_head="$(readonly_psql "$postgres" 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')"
    migration_list="$(readonly_psql "$postgres" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C";')"
    [[ -z "$migration_list" ]] || mapfile -t migrations <<< "$migration_list"
fi
[[ "$migration_count" =~ ^[1-9][0-9]*$ ]] || fail "unexpected migration count '$migration_count'"
[[ "$migration_head" =~ ^[0-9]{14}_[A-Za-z0-9]+$ ]] || fail "unexpected migration head"
[[ ${#migrations[@]} -eq $migration_count ]] || fail "migration list has ${#migrations[@]} entries but COUNT(*) is $migration_count"
[[ "${migrations[-1]}" == "$migration_head" ]] || fail "migration list does not end with the reported head"
emit migration_count "$migration_count"
emit migration_head "$migration_head"
for migration in "${migrations[@]}"; do
    [[ "$migration" =~ ^[0-9]{14}_[A-Za-z0-9]+$ ]] || fail "unexpected migration id"
    emit migration "$migration"
done
if "$INVENTORY_ONLY"; then
    for service in "${SERVICES[@]}"; do
        id="${CONTAINER[$service]}"
        [[ "$(container_for "$service")" == "$id" && "$(inspect_field "$id" '{{.State.StartedAt}}')" == "${STARTED_AT[$service]}" ]] \
            || fail "$service changed during collection"
    done
else
    row_list="$(readonly_psql "$postgres" "SELECT relname || '=' || n_live_tup FROM pg_stat_user_tables WHERE schemaname = 'public' ORDER BY relname COLLATE \"C\";")"
    row_counts=()
    [[ -z "$row_list" ]] || mapfile -t row_counts <<< "$row_list"
    for row in "${row_counts[@]}"; do
        [[ "$row" =~ ^[A-Za-z0-9_]+=[0-9]+$ ]] || fail "unexpected table statistics line"
        emit "approx_rows.${row%%=*}" "${row#*=}"
    done
fi

checksum="$(printf '%s\n' "${OUTPUT[@]}" | sha256sum | awk '{print $1}')"
printf '%s\n' "${OUTPUT[@]}"
printf 'evidence_lines_sha256=%s\n' "$checksum"
printf 'evidence_complete=true\n'
