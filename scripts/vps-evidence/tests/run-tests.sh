#!/usr/bin/env bash
# Regression tests for the VPS version-evidence scripts. Dependency-free: a mock docker CLI on PATH
# and synthetic git repositories in a temporary directory. Nothing touches Docker or a real host.
#   bash scripts/vps-evidence/tests/run-tests.sh
# The inline `bash -c '...' _ args` checks expand their positional arguments on purpose.
# shellcheck disable=SC2016
set -Eeuo pipefail

TESTS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly TESTS_DIR
readonly COLLECT="$TESTS_DIR/../collect-version-evidence.sh"
readonly MATCH="$TESTS_DIR/../match-version-evidence.sh"
WORK="$(mktemp -d "${TMPDIR:-/tmp}/vps-evidence-tests.XXXXXX")"
readonly WORK
trap 'rm -rf "$WORK"' EXIT

mkdir -p "$WORK/bin"
cp "$TESTS_DIR/mock-docker" "$WORK/bin/docker"
chmod +x "$WORK/bin/docker"
export PATH="$WORK/bin:$PATH"
export GIT_CONFIG_NOSYSTEM=1 GIT_AUTHOR_NAME=test GIT_AUTHOR_EMAIL=test@example.test \
    GIT_COMMITTER_NAME=test GIT_COMMITTER_EMAIL=test@example.test HOME="$WORK"

passed=0
failed=0
pass() { passed=$((passed + 1)); echo "PASS: $1"; }
failure() { failed=$((failed + 1)); echo "FAIL: $1" >&2; }

STATUS=0
# run <expected-exit> <command...>: captures stdout/stderr into $WORK/out and $WORK/err.
run() {
    local expected="$1"
    shift
    set +e
    "$@" > "$WORK/out" 2> "$WORK/err"
    STATUS=$?
    set -e
    [[ "$STATUS" -eq "$expected" ]]
}

check() {
    local name="$1"
    shift
    if "$@"; then pass "$name"; else
        failure "$name (exit $STATUS)"
        sed 's/^/    stderr: /' "$WORK/err" >&2 || true
    fi
}

stdout_has() { grep -qxF "$1" "$WORK/out"; }
stdout_matches() { grep -qE "$1" "$WORK/out"; }
stderr_has() { grep -qF "$1" "$WORK/err"; }
stdout_empty() { [[ ! -s "$WORK/out" ]]; }
sha() { sha256sum "$1" | awk '{print $1}'; }

# ---------------------------------------------------------------------------------------------
# Synthetic application history (linear first-parent master plus one merged side branch).
#   c1 legacy Web image (nginx.conf -> conf.d), migrations M1 M2
#   c2 Web image renders nginx.conf from templates
#   c3 adds the security-header snippet and migration M3
#   c4 documentation only (same build inputs as c3)
#   c5 changes the API appsettings
#   side: a3 then a4 on a branch, merged into master as c6 (a4)
# ---------------------------------------------------------------------------------------------
REPO="$WORK/repo"
readonly WEB=BlazorShop.Presentation/BlazorShop.Web
readonly API=BlazorShop.Presentation/BlazorShop.API
readonly SF=BlazorShop.Presentation/BlazorShop.Storefront
readonly MIG=BlazorShop.Infrastructure/Migrations
readonly M1=20260101000000_First M2=20260201000000_Second M3=20260301000000_Third

git init -q -b master "$REPO"
cd "$REPO"
mkdir -p "$WEB" "$API" "$SF" "$MIG"
add_migration() {
    printf 'migration %s\n' "$1" > "$MIG/$1.cs"
    printf 'designer %s\n' "$1" > "$MIG/$1.Designer.cs"
    printf 'snapshot after %s\n' "$1" > "$MIG/AppDbContextModelSnapshot.cs"
}
commit() { git add -A && git commit -q -m "$1" && git rev-parse HEAD; }

printf 'server { listen 80; }\n' > "$WEB/nginx.conf"
printf 'FROM nginx\nCOPY %s/nginx.conf /etc/nginx/conf.d/default.conf\nCOPY --from=build /app/publish/wwwroot /usr/share/nginx/html\n' "$WEB" > "$WEB/Dockerfile"
printf '{ "api": 1 }\n' > "$API/appsettings.json"
printf '{ "storefront": 1 }\n' > "$SF/appsettings.json"
add_migration "$M1"
add_migration "$M2"
C1="$(commit c1)"
printf 'FROM nginx\nCOPY %s/nginx.conf /etc/nginx/templates/default.conf.template\nCOPY --from=build /app/publish/wwwroot /usr/share/nginx/html\n' "$WEB" > "$WEB/Dockerfile"
C2="$(commit c2)"
printf 'add_header X-Content-Type-Options nosniff;\n' > "$WEB/nginx-security-headers.conf"
printf 'FROM nginx\nCOPY %s/nginx.conf /etc/nginx/templates/default.conf.template\nCOPY %s/nginx-security-headers.conf /etc/nginx/snippets/blazorshop-security-headers.conf\n' "$WEB" "$WEB" > "$WEB/Dockerfile"
add_migration "$M3"
C3="$(commit c3)"
printf 'docs\n' > README.md
C4="$(commit c4)"
printf '{ "api": 2 }\n' > "$API/appsettings.json"
C5="$(commit c5)"
git switch -q -c side
printf '{ "api": 3 }\n' > "$API/appsettings.json"
SIDE_A3="$(commit side-a3)"
printf '{ "api": 4 }\n' > "$API/appsettings.json"
commit side-a4 > /dev/null
git switch -q master
git merge -q --no-ff -m c6 side

H_NGINX="$(sha "$WEB/nginx.conf")"
H_HEADERS="$(sha "$WEB/nginx-security-headers.conf")"
H_SF="$(sha "$SF/appsettings.json")"
H_A1="$(git show "$C1:$API/appsettings.json" | sha256sum | awk '{print $1}')"
H_A2="$(git show "$C5:$API/appsettings.json" | sha256sum | awk '{print $1}')"
H_A3="$(git show "$SIDE_A3:$API/appsettings.json" | sha256sum | awk '{print $1}')"
readonly H_RENDERED="1111111111111111111111111111111111111111111111111111111111111111"

# evidence <file> <api> <storefront> <template> <headers> <conf.d> <migration...>: a valid v2 file.
evidence() {
    local file="$1" api="$2" storefront="$3" template="$4" headers="$5" confd="$6"
    shift 6
    local lines=(
        "schema=blazorshop-vps-version-evidence/v2"
        "collected_at_utc=2026-11-06T00:00:00Z"
        "compose_project=blazorshop"
        "checkout_head=not-collected"
        "sha256.api_appsettings=$api"
        "sha256.storefront_appsettings=$storefront"
        "sha256.web_nginx_template=$template"
        "sha256.web_security_headers=$headers"
        "sha256.web_nginx_conf_d=$confd"
        "migration_count=$#"
        "migration_head=${*: -1}"
    )
    local migration
    for migration in "$@"; do lines+=("migration=$migration"); done
    {
        printf '%s\n' "${lines[@]}"
        printf 'evidence_lines_sha256=%s\n' "$(printf '%s\n' "${lines[@]}" | sha256sum | awk '{print $1}')"
        printf 'evidence_complete=true\n'
    } > "$file"
}

# Rewrites the checksum after a deliberate edit, so only the semantic validation is exercised.
reseal() {
    local file="$1" body
    body="$(sed -n '/^evidence_lines_sha256=/q;p' "$file")"
    {
        printf '%s\n' "$body"
        printf 'evidence_lines_sha256=%s\n' "$(printf '%s\n' "$body" | sha256sum | awk '{print $1}')"
        printf 'evidence_complete=true\n'
    } > "$file"
}

E="$WORK/evidence.txt"

# ---- match-version-evidence.sh ---------------------------------------------------------------
evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"
check "matcher: several admissible commits are all reported" run 0 bash "$MATCH" "$E" master
check "matcher: the candidate set is exactly c3 and c4" bash -c '
    [[ "$(grep -c "^candidate=" "$1")" -eq 2 ]] && grep -q "^candidate=$2 " "$1" && grep -q "^candidate=$3 " "$1"' _ "$WORK/out" "$C3" "$C4"
check "matcher: valid migration head and count are echoed" stdout_has "evidence_migration_count=3"

evidence "$E" "$H_A1" "$H_SF" missing missing "$H_NGINX" "$M1" "$M2"
check "matcher: legacy conf.d Web image maps by that commit's Dockerfile" run 0 bash "$MATCH" "$E" master
check "matcher: legacy image matches only c1, not the templated c2" bash -c '[[ "$(grep -c "^candidate=" "$1")" -eq 1 ]] && grep -q "^candidate=$2 " "$1" && ! grep -q "^candidate=$3 " "$1"' _ "$WORK/out" "$C1" "$C2"

evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M3"
check "matcher: same head but a missing migration matches nothing" run 1 bash "$MATCH" "$E" master
check "matcher: migration mismatch reports zero candidates" stdout_has "candidate_count=0"

evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3" 20260401000000_Extra
check "matcher: an extra migration matches nothing" run 1 bash "$MATCH" "$E" master

evidence "$E" "$(printf 'f%.0s' {1..64})" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"
check "matcher: zero admissible commits exits 1" run 1 bash "$MATCH" "$E" master
check "matcher: zero candidates explains the scanned history" stderr_has "No commit in the scanned history"

evidence "$E" "$H_A2" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"
check "matcher: a single admissible commit is identified" run 0 bash "$MATCH" "$E" master
check "matcher: the single candidate is c5" bash -c '[[ "$(grep -c "^candidate=" "$1")" -eq 1 ]] && grep -q "^candidate=$2 " "$1"' _ "$WORK/out" "$C5"

# API state of c1/c2 (appsettings a1, migrations M1 M2) with the Web image of c3 or later.
evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2"
check "matcher: services from different commits yield no common candidate" run 1 bash "$MATCH" "$E" master
check "matcher: different-commit services are diagnosed" stderr_has "built from different commits"

evidence "$E" "$H_A3" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"
check "matcher: a side-branch state is not on the first-parent history" run 1 bash "$MATCH" "$E" master
check "matcher: --all-ancestors finds the side-branch state" run 0 bash "$MATCH" --all-ancestors "$E" master
check "matcher: --all-ancestors reports the side commit" stdout_matches "^candidate=$SIDE_A3 "

git clone -q --depth 1 "file://$REPO" "$WORK/shallow"
evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"
check "matcher: a shallow clone is refused" bash -c 'cd "$1" || exit 1; bash "$2" "$3" master > /dev/null 2> "$4"; [[ $? -eq 2 ]] && grep -q "shallow clone" "$4"' _ "$WORK/shallow" "$MATCH" "$E" "$WORK/err"

# Invalid or incomplete evidence must be rejected with exit 2 before any matching.
invalid_case() {
    local name="$1" message="$2"
    check "matcher rejects: $name" run 2 bash "$MATCH" "$E" master
    check "matcher rejects: $name (diagnostic)" stderr_has "$message"
    check "matcher rejects: $name (no candidates printed)" bash -c '! grep -q "^candidate" "$1"' _ "$WORK/out"
}
base_evidence() { evidence "$E" "$H_A1" "$H_SF" "$H_NGINX" "$H_HEADERS" "$H_RENDERED" "$M1" "$M2" "$M3"; }

base_evidence; sed -i '/^evidence_complete=true$/d' "$E"
invalid_case "missing completion marker" "incomplete file"
base_evidence; sed -i '/^migration=20260101000000_First$/d' "$E"
invalid_case "truncated or edited body" "checksum mismatch"
base_evidence; sed -i '$d' "$E"; sed -i '$d' "$E"
invalid_case "file cut before the checksum" "incomplete file"
base_evidence; sed -i 's/^migration_count=3$/migration_count=4/' "$E"; reseal "$E"
invalid_case "count differs from the migration lines" "migration_count is 4"
base_evidence; sed -i "s/^migration_head=.*/migration_head=$M2/" "$E"; reseal "$E"
invalid_case "head is not the newest migration" "not the newest migration"
base_evidence; sed -i "s/^migration=$M3\$/migration=$M2/" "$E"; reseal "$E"
invalid_case "duplicate migration ids" "duplicate migration ids"
base_evidence; sed -i 's/^compose_project=blazorshop$/compose_project=blazorshop\nsha256.api_appsettings=0000000000000000000000000000000000000000000000000000000000000000/' "$E"; reseal "$E"
invalid_case "duplicate single-valued key" "duplicate key 'sha256.api_appsettings'"
base_evidence; sed -i 's/^sha256.web_nginx_template=.*/sha256.web_nginx_template=not-a-hash/' "$E"; reseal "$E"
invalid_case "malformed hash" "sha256.web_nginx_template must be"
base_evidence; sed -i '/^sha256.storefront_appsettings=/d' "$E"; reseal "$E"
invalid_case "missing service hash" "sha256.storefront_appsettings must be"
base_evidence; sed -i 's/^schema=.*/schema=blazorshop-vps-version-evidence\/v1/' "$E"; reseal "$E"
invalid_case "old schema" "unsupported schema"
base_evidence; echo "migration=20990101000000_Late" >> "$E"
invalid_case "content after the completion marker" "content after evidence_complete"
base_evidence; sed -i 's/^compose_project=blazorshop$/compose project with spaces/' "$E"; reseal "$E"
invalid_case "malformed line" "malformed line"

# ---- collect-version-evidence.sh ----------------------------------------------------------------
export MOCK_DOCKER_DIR="$WORK/docker"
CHECKOUT="$WORK/checkout"
git clone -q "file://$REPO" "$CHECKOUT"

# mock_host [legacy]: four running service containers whose files reproduce commit c3 (or c1).
mock_host() {
    rm -rf "$MOCK_DOCKER_DIR"
    mkdir -p "$MOCK_DOCKER_DIR/psql"
    : > "$MOCK_DOCKER_DIR/calls.log"
    local service id
    for service in postgres api storefront web; do
        id="${service}0000000000000000000000000000000000000000000000000000000000"
        id="${id:0:64}"
        echo "$id|blazorshop|$service|running|False|sha256:img-$service|blazorshop-$service|2026-11-01T10:00:00Z|/srv/blazorshop" >> "$MOCK_DOCKER_DIR/containers"
        echo "sha256:img-$service|2026-11-01T09:00:00Z" >> "$MOCK_DOCKER_DIR/images"
        mkdir -p "$MOCK_DOCKER_DIR/fs/$id"
    done
    API_ID="$(container_id api)"
    WEB_ID="$(container_id web)"
    mkdir -p "$MOCK_DOCKER_DIR/fs/$API_ID/app/uploads/products" "$MOCK_DOCKER_DIR/fs/$API_ID/root/.aspnet/DataProtection-Keys"
    git -C "$REPO" show "$C3:$API/appsettings.json" > "$MOCK_DOCKER_DIR/fs/$API_ID/app/appsettings.json"
    echo upload > "$MOCK_DOCKER_DIR/fs/$API_ID/app/uploads/products/a.png"
    echo key > "$MOCK_DOCKER_DIR/fs/$API_ID/root/.aspnet/DataProtection-Keys/key-1.xml"
    mkdir -p "$MOCK_DOCKER_DIR/fs/$(container_id storefront)/app"
    git -C "$REPO" show "$C3:$SF/appsettings.json" > "$MOCK_DOCKER_DIR/fs/$(container_id storefront)/app/appsettings.json"
    mkdir -p "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/templates" "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/snippets" "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/conf.d"
    git -C "$REPO" show "$C3:$WEB/nginx.conf" > "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/templates/default.conf.template"
    git -C "$REPO" show "$C3:$WEB/nginx-security-headers.conf" > "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/snippets/blazorshop-security-headers.conf"
    echo "server { listen 80; server_name rendered.example.test; }" > "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/conf.d/default.conf"
    echo "16.13" > "$MOCK_DOCKER_DIR/psql/version"
    echo "123456" > "$MOCK_DOCKER_DIR/psql/size"
    echo "3" > "$MOCK_DOCKER_DIR/psql/count"
    echo "$M3" > "$MOCK_DOCKER_DIR/psql/head"
    printf '%s\n' "$M1" "$M2" "$M3" > "$MOCK_DOCKER_DIR/psql/list"
    printf '%s\n' "AspNetUsers=2" "Orders=5" > "$MOCK_DOCKER_DIR/psql/rows"
}
container_id() { local id="${1}0000000000000000000000000000000000000000000000000000000000"; printf '%s' "${id:0:64}"; }
collect() { bash "$COLLECT" blazorshop "$CHECKOUT"; }

mock_host
check "collector: one running container per service succeeds" run 0 collect
check "collector: a clean run writes nothing to stderr" bash -c '[[ ! -s "$1" ]]' _ "$WORK/err"
check "collector: output ends with the completion marker" bash -c '[[ "$(tail -n 1 "$1")" == "evidence_complete=true" ]]' _ "$WORK/out"
check "collector: hashes the copied API appsettings" stdout_has "sha256.api_appsettings=$H_A1"
check "collector: reports migration head and count" bash -c 'grep -qx "migration_count=3" "$1" && grep -qx "migration_head=$2" "$1"' _ "$WORK/out" "$M3"
check "collector: reports the checkout as context" stdout_has "checkout_head=$(git -C "$CHECKOUT" rev-parse HEAD)"
check "collector: issues only read-only docker commands" bash -c '! grep -vE "^(ps|inspect|image inspect|exec) " "$1"' _ "$MOCK_DOCKER_DIR/calls.log"
check "collector: never runs docker compose or lifecycle commands" bash -c '! grep -qE "^(compose|restart|start|stop|kill|rm|run|pull|build|cp|commit|volume|network|update) " "$1"' _ "$MOCK_DOCKER_DIR/calls.log"
check "collector: queries PostgreSQL read-only without password prompts" bash -c 'grep "psql" "$1" | grep -q "default_transaction_read_only=on" && ! grep "psql" "$1" | grep -qv -- " -w "' _ "$MOCK_DOCKER_DIR/calls.log"
check "collector: prints no file contents" bash -c '! grep -qE "rendered.example.test|\"api\"|^key$" "$1"' _ "$WORK/out"
cp "$WORK/out" "$WORK/collected.txt"
check "collector + matcher: collected evidence matches c3 and c4" bash -c 'cd "$1" && bash "$2" "$3" master > "$4" 2>&1 && [[ "$(grep -c "^candidate=" "$4")" -eq 2 ]]' _ "$REPO" "$MATCH" "$WORK/collected.txt" "$WORK/match.txt"

mock_host
sed -i '/|api|/d' "$MOCK_DOCKER_DIR/containers"
check "collector: missing container fails" run 1 collect
check "collector: missing container is diagnosed" stderr_has "no running 'api' container"
check "collector: missing container prints no evidence" stdout_empty

mock_host
echo "api-second-0000000000000000000000000000000000000000000000000000000|blazorshop|api|running|False|sha256:img-api|blazorshop-api|2026-11-02T10:00:00Z|/srv/blazorshop" >> "$MOCK_DOCKER_DIR/containers"
check "collector: two running containers for one service fail" run 1 collect
check "collector: duplicate containers are diagnosed" stderr_has "2 running 'api' containers"
check "collector: duplicate containers print no evidence" stdout_empty

mock_host
sed -i 's/^\([^|]*|blazorshop|api|\)running|/\1exited|/' "$MOCK_DOCKER_DIR/containers"
check "collector: a stopped container is not used" run 1 collect
check "collector: stopped container is diagnosed" stderr_has "no running 'api' container in Compose project 'blazorshop' (1 non-running)"

mock_host
echo "api-stopped-000000000000000000000000000000000000000000000000000000|blazorshop|api|exited|False|sha256:img-old|blazorshop-api|2026-10-01T10:00:00Z|/srv/blazorshop" >> "$MOCK_DOCKER_DIR/containers"
echo "api-oneoff-0000000000000000000000000000000000000000000000000000000|blazorshop|api|running|True|sha256:img-old|blazorshop-api|2026-11-03T10:00:00Z|/srv/blazorshop" >> "$MOCK_DOCKER_DIR/containers"
check "collector: stopped and one-off containers are ignored" run 0 collect
check "collector: the running service container is selected" stdout_has "api_container_id=$(container_id api)"

mock_host
rm "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/snippets/blazorshop-security-headers.conf"
check "collector: an absent file is reported as missing" run 0 collect
check "collector: missing means provably absent" stdout_has "sha256.web_security_headers=missing"
check "collector: a provably absent file raises no failure diagnostic" bash -c '[[ ! -s "$1" ]]' _ "$WORK/err"

mock_host
echo "$WEB_ID" > "$MOCK_DOCKER_DIR/exec-fail"
check "collector: failed docker exec aborts" run 1 collect
check "collector: failed docker exec is not reported as missing" stdout_empty
check "collector: failed docker exec is diagnosed" stderr_has "could not hash"

mock_host
rm "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/conf.d/default.conf"
mkdir "$MOCK_DOCKER_DIR/fs/$WEB_ID/etc/nginx/conf.d/default.conf"
check "collector: a non-regular file aborts" run 1 collect
check "collector: a non-regular file prints no evidence" stdout_empty

mock_host
touch "$MOCK_DOCKER_DIR/psql-fail"
check "collector: a PostgreSQL failure aborts" run 1 collect
check "collector: a PostgreSQL failure prints no evidence" stdout_empty

mock_host
printf '%s\n' "$M1" "$M3" > "$MOCK_DOCKER_DIR/psql/list"
check "collector: inconsistent migration count and list abort" run 1 collect
check "collector: inconsistent migrations are diagnosed" stderr_has "migration list has 2 entries but COUNT(*) is 3"

mock_host
check "collector: a non-git checkout directory aborts" run 1 bash "$COLLECT" blazorshop "$WORK/bin"

mock_host
check "collector: without arguments it only lists projects" run 2 bash "$COLLECT"
check "collector: lists the running Compose project" stderr_has "  blazorshop"

mock_host
check "collector: an invalid project name is refused" run 1 bash "$COLLECT" 'Blazor Shop'

echo "passed=$passed failed=$failed"
[[ $failed -eq 0 ]]
