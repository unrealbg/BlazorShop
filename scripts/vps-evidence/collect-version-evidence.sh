#!/usr/bin/env bash
# Read-only evidence of the BlazorShop version that is actually running on a Docker Compose host.
#
# Run it on the host, in the deployment checkout, by an operator with Docker access:
#   bash scripts/vps-evidence/collect-version-evidence.sh <compose-project-name> > vps-evidence.txt
# Without a project name it only lists the Compose projects that are running.
#
# It never changes anything: no compose up/down/pull/build, no restart, no writes to containers,
# volumes or the database. PostgreSQL queries run in read-only transactions. It prints no
# environment variables, connection strings, keys, file contents or row data; only commit IDs,
# image IDs and timestamps, SHA-256 hashes of files that are copied verbatim from the repository
# into the images, migration IDs, sizes, counts and versions.
# Review the output before sharing it.
set -Eeuo pipefail

readonly DB_NAME="${BLAZORSHOP_EVIDENCE_DB_NAME:-blazorshop}"
readonly DB_USER="${BLAZORSHOP_EVIDENCE_DB_USER:-blazorshop}"

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <compose-project-name>" >&2
    echo "Running Compose projects:" >&2
    docker ps --format '{{.Label "com.docker.compose.project"}}' | sed '/^$/d' | sort -u | sed 's/^/  /' >&2
    exit 2
fi
readonly PROJECT="$1"

container_for() {
    docker ps --all --quiet \
        --filter "label=com.docker.compose.project=$PROJECT" \
        --filter "label=com.docker.compose.service=$1" | head -n 1
}

# Prints "<sha256>" of a file inside a running container, or "missing".
file_sha256() {
    local container="$1"
    local path="$2"
    local hash
    hash="$(docker exec "$container" sha256sum "$path" 2>/dev/null | awk '{print $1}' || true)"
    printf '%s\n' "${hash:-missing}"
}

readonly_psql() {
    docker exec --env PGOPTIONS='-c default_transaction_read_only=on' "$1" \
        psql -X --set ON_ERROR_STOP=1 --tuples-only --no-align -U "$DB_USER" -d "$DB_NAME" --command "$2"
}

echo "schema=blazorshop-vps-version-evidence/v1"
echo "collected_at_utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "compose_project=$PROJECT"

echo "# checkout (the current directory; not proof of what the containers run)"
if git rev-parse --is-inside-work-tree > /dev/null 2>&1; then
    echo "checkout_head=$(git rev-parse HEAD)"
    echo "checkout_head_commit_time=$(git log -1 --format=%cI)"
    echo "checkout_branch=$(git rev-parse --abbrev-ref HEAD)"
    echo "checkout_modified_files=$(git status --porcelain --untracked-files=no | wc -l | tr -d ' ')"
    git status --porcelain --untracked-files=no | awk '{print "checkout_modified=" $2}'
else
    echo "checkout_head=not-a-git-checkout"
fi

echo "# containers"
for service in postgres api storefront web; do
    id="$(container_for "$service")"
    if [[ -z "$id" ]]; then
        echo "${service}_container=missing"
        continue
    fi
    image_id="$(docker inspect --format '{{.Image}}' "$id")"
    echo "${service}_state=$(docker inspect --format '{{.State.Status}}' "$id")"
    echo "${service}_started_at=$(docker inspect --format '{{.State.StartedAt}}' "$id")"
    echo "${service}_image_ref=$(docker inspect --format '{{.Config.Image}}' "$id")"
    echo "${service}_image_id=$image_id"
    echo "${service}_image_created=$(docker image inspect --format '{{.Created}}' "$image_id")"
done

api="$(container_for api)"
storefront="$(container_for storefront)"
web="$(container_for web)"
postgres="$(container_for postgres)"

echo "# files copied verbatim from the repository into the running images"
[[ -n "$api" ]] && echo "sha256.api_appsettings=$(file_sha256 "$api" /app/appsettings.json)"
[[ -n "$storefront" ]] && echo "sha256.storefront_appsettings=$(file_sha256 "$storefront" /app/appsettings.json)"
[[ -n "$web" ]] && echo "sha256.web_nginx_template=$(file_sha256 "$web" /etc/nginx/templates/default.conf.template)"
[[ -n "$web" ]] && echo "sha256.web_security_headers=$(file_sha256 "$web" /etc/nginx/snippets/blazorshop-security-headers.conf)"

if [[ -n "$api" ]]; then
    echo "# persistent volumes (sizes and counts only)"
    echo "uploads_kib=$(docker exec "$api" du -sk /app/uploads 2>/dev/null | awk '{print $1}' || true)"
    echo "uploads_files=$(docker exec "$api" find /app/uploads -type f 2>/dev/null | wc -l | tr -d ' ')"
    echo "data_protection_key_files=$(docker exec "$api" find /root/.aspnet/DataProtection-Keys -type f 2>/dev/null | wc -l | tr -d ' ')"
fi

if [[ -n "$postgres" ]]; then
    echo "# database (read-only transactions)"
    echo "postgres_server_version=$(readonly_psql "$postgres" 'SHOW server_version;')"
    echo "database_size_bytes=$(readonly_psql "$postgres" 'SELECT pg_database_size(current_database());')"
    echo "migration_count=$(readonly_psql "$postgres" 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')"
    echo "migration_head=$(readonly_psql "$postgres" 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')"
    readonly_psql "$postgres" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";' | sed 's/^/migration=/'
    readonly_psql "$postgres" "SELECT relname || '=' || n_live_tup FROM pg_stat_user_tables WHERE schemaname = 'public' ORDER BY relname COLLATE \"C\";" \
        | sed 's/^/approx_rows./'
fi
