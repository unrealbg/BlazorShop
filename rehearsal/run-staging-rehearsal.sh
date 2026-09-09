#!/usr/bin/env bash
set -Eeuo pipefail

readonly BASELINE_SHA="df8a5e19d12728f0228e293404d6e2ceee743db7"
readonly TARGET_SHA="8370f817857648f475041228ea6eaf228a3b9e34"
readonly POSTGRES_IMAGE="postgres:16.13-alpine3.23"
readonly DOTNET_SDK_IMAGE="mcr.microsoft.com/dotnet/sdk:10.0.202-noble"
readonly DOTNET_ASPNET_IMAGE="mcr.microsoft.com/dotnet/aspnet:10.0.6-noble"
readonly NGINX_IMAGE="nginx:1.27.5-alpine3.21"
readonly CATEGORY_ID="37000000-0000-0000-0000-000000000001"
readonly SIMPLE_PRODUCT_ID="37000000-0000-0000-0000-000000000010"
readonly VARIANT_PRODUCT_ID="37000000-0000-0000-0000-000000000020"
readonly VARIANT_ID="37000000-0000-0000-0000-000000000021"

if [[ $# -ne 3 ]]; then
    echo "Usage: $0 <tooling-directory> <baseline-source-directory> <target-source-directory>" >&2
    exit 2
fi

readonly TOOLING_DIR="$(realpath "$1")"
readonly BASELINE_DIR="$(realpath "$2")"
readonly TARGET_DIR="$(realpath "$3")"
readonly COMPOSE_FILE="$TOOLING_DIR/rehearsal/compose.staging-rehearsal.yml"
readonly ARTIFACT_DIR="$TOOLING_DIR/artifacts/staging-rehearsal"
readonly LOG_DIR="$ARTIFACT_DIR/logs"
readonly REPORT_DIR="$ARTIFACT_DIR/reports"
readonly TEST_RESULTS_DIR="$ARTIFACT_DIR/browser-results"
readonly RUN_NUMBER="${GITHUB_RUN_ID:-local}"
readonly RUN_ATTEMPT="${GITHUB_RUN_ATTEMPT:-1}"
readonly PROJECT_NAME="bsrehearsal${RUN_NUMBER//[^0-9]/0}a${RUN_ATTEMPT//[^0-9]/0}"
readonly PORT_SEED="$(printf '%s' "${GITHUB_RUN_ID:-0}" | tr -cd '0-9' | tail -c 4)"
readonly PORT_OFFSET="$((10#${PORT_SEED:-0} % 15000))"
readonly RUNTIME_DIR="$(mktemp -d "${RUNNER_TEMP:-/tmp}/blazorshop-rehearsal.XXXXXX")"
readonly BACKUP_PATH="$RUNTIME_DIR/baseline.dump"
readonly LOGIN_RESPONSE_PATH="$RUNTIME_DIR/login-response.json"
readonly PROTECTED_RESPONSE_PATH="$RUNTIME_DIR/protected-response.json"

mkdir -p "$LOG_DIR" "$REPORT_DIR" "$TEST_RESULTS_DIR"

generate_user_password() {
    local candidate
    while true; do
        candidate="$(openssl rand -base64 32 | tr -d '\n')"
        if [[ "$candidate" =~ [[:lower:]] \
            && "$candidate" =~ [[:upper:]] \
            && "$candidate" =~ [[:digit:]] \
            && "$candidate" =~ [^[:alnum:]] ]]; then
            printf '%s' "$candidate"
            return 0
        fi
    done
}

export REHEARSAL_POSTGRES_IMAGE="$POSTGRES_IMAGE"
export REHEARSAL_DB_NAME="blazorshop_rehearsal"
export REHEARSAL_DB_USER="rehearsal"
export REHEARSAL_DB_PASSWORD="$(openssl rand -hex 32)"
export REHEARSAL_JWT_KEY="$(openssl rand -base64 48 | tr -d '\n')"
export REHEARSAL_API_PORT="$((21000 + PORT_OFFSET))"
export REHEARSAL_STOREFRONT_PORT="$((REHEARSAL_API_PORT + 1))"
export REHEARSAL_WEB_PORT="$((REHEARSAL_API_PORT + 2))"
export REHEARSAL_API_URL="http://localhost:$REHEARSAL_API_PORT"
export REHEARSAL_STOREFRONT_URL="http://localhost:$REHEARSAL_STOREFRONT_PORT"
export REHEARSAL_WEB_URL="http://localhost:$REHEARSAL_WEB_PORT"
export REHEARSAL_DB_HOST="postgres"

readonly USER_EMAIL="rehearsal.user.${RUN_NUMBER}.${RUN_ATTEMPT}@example.test"
readonly USER_PASSWORD="$(generate_user_password)"
readonly BASELINE_IMAGE_PREFIX="$PROJECT_NAME-baseline"
readonly TARGET_IMAGE_PREFIX="$PROJECT_NAME-target"
readonly BASELINE_API_IMAGE="$BASELINE_IMAGE_PREFIX-api:local"
readonly BASELINE_STOREFRONT_IMAGE="$BASELINE_IMAGE_PREFIX-storefront:local"
readonly BASELINE_WEB_IMAGE="$BASELINE_IMAGE_PREFIX-web:local"
readonly TARGET_API_IMAGE="$TARGET_IMAGE_PREFIX-api:local"
readonly TARGET_STOREFRONT_IMAGE="$TARGET_IMAGE_PREFIX-storefront:local"
readonly TARGET_WEB_IMAGE="$TARGET_IMAGE_PREFIX-web:local"

# Compose evaluates required image variables even during best-effort diagnostics
# and teardown. Start with the baseline images so an early failure remains cleanable.
export REHEARSAL_API_IMAGE="$BASELINE_API_IMAGE"
export REHEARSAL_STOREFRONT_IMAGE="$BASELINE_STOREFRONT_IMAGE"
export REHEARSAL_WEB_IMAGE="$BASELINE_WEB_IMAGE"

UPGRADE_MILLISECONDS="not-run"
RESTORE_MILLISECONDS="not-run"
REHEARSAL_RESULT="failed"

dc() {
    docker compose --project-name "$PROJECT_NAME" --file "$COMPOSE_FILE" "$@"
}

capture_logs() {
    local phase="$1"
    if ! dc --profile restore logs --no-color --timestamps > "$LOG_DIR/${phase}-compose.log" 2>&1; then
        echo "Unable to capture complete Compose logs for phase $phase." >> "$LOG_DIR/${phase}-compose.log"
    fi
    dc --profile restore ps --all > "$LOG_DIR/${phase}-compose-ps.txt" 2>&1 || :
}

cleanup() {
    local original_exit="$?"
    trap - EXIT
    set +e

    capture_logs "final"
    local cleanup_started
    cleanup_started="$(date +%s%3N)"
    dc --profile restore down --volumes --remove-orphans --timeout 20 > "$LOG_DIR/cleanup.log" 2>&1
    local compose_cleanup_exit="$?"

    docker image rm \
        "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE" \
        "$TARGET_API_IMAGE" "$TARGET_STOREFRONT_IMAGE" "$TARGET_WEB_IMAGE" \
        >> "$LOG_DIR/cleanup.log" 2>&1
    local image_cleanup_exit="$?"

    rm -rf "$RUNTIME_DIR"
    local runtime_cleanup_exit="$?"
    local cleanup_milliseconds="$(( $(date +%s%3N) - cleanup_started ))"

    {
        echo "compose_project=$PROJECT_NAME"
        echo "compose_down_exit=$compose_cleanup_exit"
        echo "image_cleanup_exit=$image_cleanup_exit"
        echo "runtime_cleanup_exit=$runtime_cleanup_exit"
        echo "duration_milliseconds=$cleanup_milliseconds"
    } > "$REPORT_DIR/cleanup.txt"

    if [[ "$compose_cleanup_exit" -ne 0 || "$image_cleanup_exit" -ne 0 || "$runtime_cleanup_exit" -ne 0 ]]; then
        original_exit=1
    fi

    {
        echo "result=$REHEARSAL_RESULT"
        echo "baseline_sha=$BASELINE_SHA"
        echo "target_sha=$TARGET_SHA"
        echo "upgrade_duration_milliseconds=$UPGRADE_MILLISECONDS"
        echo "restore_duration_milliseconds=$RESTORE_MILLISECONDS"
        echo "cleanup_duration_milliseconds=$cleanup_milliseconds"
    } > "$REPORT_DIR/result.txt"

    exit "$original_exit"
}
trap cleanup EXIT

run_logged() {
    local log_path="$1"
    shift
    set +e
    "$@" 2>&1 | tee "$log_path"
    local command_exit="${PIPESTATUS[0]}"
    set -e
    if [[ "$command_exit" -ne 0 ]]; then
        echo "Command failed with exit code $command_exit; see $log_path." >&2
        return "$command_exit"
    fi
}

wait_http() {
    local url="$1"
    local label="$2"
    for _ in $(seq 1 90); do
        if curl --fail --silent --show-error --max-time 5 "$url" > /dev/null; then
            echo "$label is ready at $url."
            return 0
        fi
        sleep 2
    done
    echo "$label did not become ready at $url within 180 seconds." >&2
    return 1
}

wait_postgres() {
    local service="$1"
    for _ in $(seq 1 60); do
        if dc --profile restore exec -T "$service" pg_isready -U "$REHEARSAL_DB_USER" -d "$REHEARSAL_DB_NAME" > /dev/null 2>&1; then
            return 0
        fi
        sleep 2
    done
    echo "PostgreSQL service $service did not become ready within 120 seconds." >&2
    return 1
}

psql_service() {
    local service="$1"
    shift
    dc --profile restore exec -T "$service" \
        psql -X --set ON_ERROR_STOP=1 -U "$REHEARSAL_DB_USER" -d "$REHEARSAL_DB_NAME" "$@"
}

scalar() {
    local service="$1"
    local sql="$2"
    psql_service "$service" --tuples-only --no-align --command "$sql" | tr -d '\r' | sed '/^[[:space:]]*$/d'
}

assert_equal() {
    local expected="$1"
    local actual="$2"
    local description="$3"
    if [[ "$actual" != "$expected" ]]; then
        echo "Assertion failed for $description. Expected '$expected', received '$actual'." >&2
        return 1
    fi
    echo "PASS: $description"
}

capture_baseline_report() {
    local service="$1"
    local output="$2"
    {
        echo "application_sha=$BASELINE_SHA"
        echo "schema=historical-baseline"
        echo "migration_history"
        scalar "$service" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
        echo "user"
        scalar "$service" "SELECT \"Id\", \"Email\", \"FullName\", \"EmailConfirmed\" FROM \"AspNetUsers\" WHERE \"Email\" = '$USER_EMAIL';"
        echo "category"
        scalar "$service" "SELECT \"Id\", \"Name\", \"Slug\", \"IsPublished\" FROM \"Categories\" WHERE \"Id\" = '$CATEGORY_ID';"
        echo "products"
        scalar "$service" "SELECT \"Id\", \"Name\", \"Slug\", to_char(\"Price\", 'FM999999990.00'), \"Quantity\", \"IsPublished\" FROM \"Products\" WHERE \"Id\" IN ('$SIMPLE_PRODUCT_ID', '$VARIANT_PRODUCT_ID') ORDER BY \"Id\";"
        echo "variant"
        scalar "$service" "SELECT \"Id\", \"ProductId\", \"Sku\", \"SizeScale\", \"SizeValue\", to_char(\"Price\", 'FM999999990.00'), \"Stock\", \"Color\", \"IsDefault\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID';"
        echo "orders"
        scalar "$service" "SELECT \"Id\", \"UserId\", \"Status\", \"Reference\", to_char(\"TotalAmount\", 'FM999999990.00'), \"ShippingStatus\", COALESCE(to_char(\"ShippedOn\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"'), '<null>'), COALESCE(to_char(\"DeliveredOn\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"'), '<null>') FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
        echo "order_lines"
        scalar "$service" "SELECT \"Id\", \"OrderId\", \"ProductId\", \"Quantity\", to_char(\"UnitPrice\", 'FM999999990.00') FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
        echo "checkout_history"
        scalar "$service" "SELECT \"Id\", \"ProductId\", \"Quantity\", \"UserId\", to_char(\"CreatedOn\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') FROM \"CheckoutOrderItems\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
    } > "$output"
}

verify_baseline_database() {
    local service="$1"
    assert_equal "5" "$(scalar "$service" 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" "baseline migration count"
    assert_equal "20260424183749_AdminBackOfficeMvp" "$(scalar "$service" 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')" "baseline migration head"
    assert_equal "Rehearsal Footwear|rehearsal-footwear|t" "$(scalar "$service" "SELECT \"Name\", \"Slug\", \"IsPublished\" FROM \"Categories\" WHERE \"Id\" = '$CATEGORY_ID';")" "baseline catalog marker"
    assert_equal "19|0|7" "$(scalar "$service" "SELECT (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$SIMPLE_PRODUCT_ID'), (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$VARIANT_PRODUCT_ID'), (SELECT \"Stock\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID');")" "baseline inventory values"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order count"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order-line count"
    assert_equal "Paid|delivered|25.00|Pending|OutForDelivery|62.50" "$(scalar "$service" "SELECT string_agg(\"Status\" || '|' || \"ShippingStatus\" || '|' || to_char(\"TotalAmount\", 'FM999999990.00'), '|' ORDER BY \"Id\") FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order lifecycle and totals"
    assert_equal "87.50" "$(scalar "$service" "SELECT to_char(SUM(\"UnitPrice\" * \"Quantity\"), 'FM999999990.00') FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order-line total"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"CheckoutOrderItems\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline checkout-history count"
    assert_equal "4" "$(scalar "$service" "SELECT SUM(\"Quantity\") FROM \"CheckoutOrderItems\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline checkout-history quantities"
}

verify_target_database() {
    assert_equal "12" "$(scalar postgres 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" "target migration count"
    assert_equal "20260909075202_ArchiveLegacyCheckoutOrderItems" "$(scalar postgres 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')" "target migration head"
    assert_equal "yes|no" "$(scalar postgres "SELECT CASE WHEN to_regclass('\"LegacyCheckoutOrderItemsArchive\"') IS NOT NULL THEN 'yes' ELSE 'no' END, CASE WHEN to_regclass('\"CheckoutOrderItems\"') IS NOT NULL THEN 'yes' ELSE 'no' END;")" "archive rename"
    assert_equal "Operational archive of legacy checkout-history rows. Not an active order-history source." "$(scalar postgres "SELECT obj_description('\"LegacyCheckoutOrderItemsArchive\"'::regclass);")" "archive diagnostic comment"
    assert_equal "2" "$(scalar postgres "SELECT COUNT(*) FROM \"LegacyCheckoutOrderItemsArchive\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "archived legacy row count"
    assert_equal "4" "$(scalar postgres "SELECT SUM(\"Quantity\") FROM \"LegacyCheckoutOrderItemsArchive\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "archived legacy quantities"
    assert_equal "19|0|7" "$(scalar postgres "SELECT (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$SIMPLE_PRODUCT_ID'), (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$VARIANT_PRODUCT_ID'), (SELECT \"Stock\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID');")" "unchanged inventory values"
    assert_equal "2|2|87.50|87.50" "$(scalar postgres "SELECT (SELECT COUNT(*) FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%'), (SELECT COUNT(*) FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%'), to_char((SELECT SUM(\"TotalAmount\") FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%'), 'FM999999990.00'), to_char((SELECT SUM(\"LineTotal\") FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%'), 'FM999999990.00');")" "preserved order and line counts and totals"
    assert_equal "Paid|delivered|Pending|Unknown|Delivered|Pending|25.00" "$(scalar postgres "SELECT \"LegacyStatus\", \"LegacyShippingStatus\", \"PaymentStatus\", \"PaymentMethod\", \"FulfillmentStatus\", \"OrderStatus\", to_char(\"SubtotalAmount\", 'FM999999990.00') FROM \"Orders\" WHERE \"Id\" = '37000000-0000-0000-0000-000000000100';")" "conservative paid-label mapping"
    assert_equal "Pending|OutForDelivery|Pending|Unknown|OutForDelivery|Pending|62.50" "$(scalar postgres "SELECT \"LegacyStatus\", \"LegacyShippingStatus\", \"PaymentStatus\", \"PaymentMethod\", \"FulfillmentStatus\", \"OrderStatus\", to_char(\"SubtotalAmount\", 'FM999999990.00') FROM \"Orders\" WHERE \"Id\" = '37000000-0000-0000-0000-000000000200';")" "legacy fulfillment mapping"
    assert_equal "Rehearsal Canvas Bag|25.00|<null>|<null>|<null>|<null>" "$(scalar postgres "SELECT \"ProductNameSnapshot\", to_char(\"LineTotal\", 'FM999999990.00'), COALESCE(\"SkuSnapshot\", '<null>'), COALESCE(\"SizeScaleSnapshot\", '<null>'), COALESCE(\"SizeValueSnapshot\", '<null>'), COALESCE(\"ColorSnapshot\", '<null>') FROM \"OrderLines\" WHERE \"Id\" = '37000000-0000-0000-0000-000000000101';")" "simple order-line snapshot"
    assert_equal "Rehearsal Trail Shoe|62.50|<null>|<null>|<null>|<null>" "$(scalar postgres "SELECT \"ProductNameSnapshot\", to_char(\"LineTotal\", 'FM999999990.00'), COALESCE(\"SkuSnapshot\", '<null>'), COALESCE(\"SizeScaleSnapshot\", '<null>'), COALESCE(\"SizeValueSnapshot\", '<null>'), COALESCE(\"ColorSnapshot\", '<null>') FROM \"OrderLines\" WHERE \"Id\" = '37000000-0000-0000-0000-000000000201';")" "legacy variant-identity limitation"
    assert_equal "0|0|0" "$(scalar postgres 'SELECT (SELECT COUNT(*) FROM "PaymentTransactions"), (SELECT COUNT(*) FROM "InventoryReservations"), (SELECT COUNT(*) FROM "CheckoutIdempotencyRecords");')" "no invented payment, reservation, or idempotency rows"
}

capture_target_report() {
    {
        echo "application_sha=$TARGET_SHA"
        echo "schema=upgraded-historical-database"
        echo "migration_history"
        scalar postgres 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";'
        echo "catalog_and_inventory"
        scalar postgres "SELECT \"Id\", \"Name\", \"Slug\", to_char(\"Price\", 'FM999999990.00'), \"Quantity\", \"IsPublished\" FROM \"Products\" WHERE \"Id\" IN ('$SIMPLE_PRODUCT_ID', '$VARIANT_PRODUCT_ID') ORDER BY \"Id\";"
        scalar postgres "SELECT \"Id\", \"ProductId\", \"Sku\", \"SizeScale\", \"SizeValue\", to_char(\"Price\", 'FM999999990.00'), \"Stock\", \"Color\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID';"
        echo "orders_after_mapping"
        scalar postgres "SELECT \"Id\", \"LegacyStatus\", \"LegacyShippingStatus\", \"OrderStatus\", \"PaymentStatus\", \"PaymentMethod\", \"FulfillmentStatus\", to_char(\"SubtotalAmount\", 'FM999999990.00'), to_char(\"TotalAmount\", 'FM999999990.00') FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
        echo "order_line_snapshots"
        scalar postgres "SELECT \"Id\", \"ProductNameSnapshot\", COALESCE(\"ProductVariantId\"::text, '<null>'), COALESCE(\"SkuSnapshot\", '<null>'), COALESCE(\"SizeScaleSnapshot\", '<null>'), COALESCE(\"SizeValueSnapshot\", '<null>'), COALESCE(\"ColorSnapshot\", '<null>'), to_char(\"UnitPrice\", 'FM999999990.00'), to_char(\"LineTotal\", 'FM999999990.00') FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
        echo "legacy_archive"
        scalar postgres "SELECT \"Id\", \"ProductId\", \"Quantity\", \"UserId\", to_char(\"CreatedOn\" AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') FROM \"LegacyCheckoutOrderItemsArchive\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%' ORDER BY \"Id\";"
        echo "commerce_side_effect_counts"
        scalar postgres 'SELECT (SELECT COUNT(*) FROM "PaymentTransactions"), (SELECT COUNT(*) FROM "InventoryReservations"), (SELECT COUNT(*) FROM "CheckoutIdempotencyRecords");'
    } > "$REPORT_DIR/post-upgrade-control.txt"
}

register_user() {
    local payload
    payload="$(jq --compact-output --null-input \
        --arg email "$USER_EMAIL" \
        --arg password "$USER_PASSWORD" \
        '{fullName:"Rehearsal Customer",email:$email,password:$password,confirmPassword:$password}')"
    local status
    status="$(curl --silent --show-error --output "$RUNTIME_DIR/register-response.json" --write-out '%{http_code}' \
        --header 'Content-Type: application/json' --request POST --data "$payload" \
        "$REHEARSAL_API_URL/api/authentication/create")"
    assert_equal "200" "$status" "historical user registration"
    assert_equal "true" "$(jq --raw-output '.success' "$RUNTIME_DIR/register-response.json")" "historical user registration result"
}

verify_baseline_application() {
    local phase="$1"
    wait_http "$REHEARSAL_API_URL/health" "$phase baseline API"
    wait_http "$REHEARSAL_STOREFRONT_URL/product/rehearsal-canvas-bag" "$phase baseline Storefront"
    wait_http "$REHEARSAL_WEB_URL/authentication/login/account" "$phase baseline Web"

    local product_page="$RUNTIME_DIR/${phase}-product.html"
    curl --fail --silent --show-error \
        "$REHEARSAL_STOREFRONT_URL/product/rehearsal-canvas-bag" --output "$product_page"
    grep --fixed-strings --quiet "Rehearsal Canvas Bag" "$product_page"
    rm -f "$product_page"

    local login_payload
    login_payload="$(jq --compact-output --null-input --arg email "$USER_EMAIL" --arg password "$USER_PASSWORD" \
        '{email:$email,password:$password}')"
    curl --fail --silent --show-error --header 'Content-Type: application/json' \
        --request POST --data "$login_payload" "$REHEARSAL_API_URL/api/authentication/login" \
        --output "$LOGIN_RESPONSE_PATH"
    local access_token
    access_token="$(jq --exit-status --raw-output '.token' "$LOGIN_RESPONSE_PATH")"
    curl --fail --silent --show-error --header "Authorization: Bearer $access_token" \
        "$REHEARSAL_API_URL/api/cart/user/orders" --output "$PROTECTED_RESPONSE_PATH"
    jq --exit-status 'map(select(.reference == "REHEARSAL-LEGACY-001")) | length == 1' \
        "$PROTECTED_RESPONSE_PATH" > /dev/null
    rm -f "$LOGIN_RESPONSE_PATH" "$PROTECTED_RESPONSE_PATH"
    echo "PASS: $phase baseline catalog, login, and protected order-history operation"
}

stop_application_writers() {
    dc stop --timeout 20 storefront web api
    local running
    running="$(dc ps --services --status running | grep -E '^(api|storefront|web)$' || :)"
    if [[ -n "$running" ]]; then
        echo "Application writers remain active after stop: $running" >&2
        return 1
    fi
    echo "PASS: all application writers are stopped"
}

build_image() {
    local source_dir="$1"
    local dockerfile="$2"
    local image="$3"
    local log_name="$4"
    run_logged "$LOG_DIR/$log_name" timeout 1200 docker build --pull \
        --tag "$image" --file "$source_dir/$dockerfile" "$source_dir"
}

baseline_actual="$(git -C "$BASELINE_DIR" rev-parse HEAD)"
target_actual="$(git -C "$TARGET_DIR" rev-parse HEAD)"
assert_equal "$BASELINE_SHA" "$baseline_actual" "baseline application source SHA"
assert_equal "$TARGET_SHA" "$target_actual" "target application source SHA"
assert_equal "" "$(git -C "$BASELINE_DIR" status --porcelain)" "clean baseline source checkout"
assert_equal "" "$(git -C "$TARGET_DIR" status --porcelain)" "clean target source checkout"

run_logged "$LOG_DIR/pull-postgres.log" docker pull "$POSTGRES_IMAGE"
run_logged "$LOG_DIR/pull-dotnet-sdk.log" docker pull "$DOTNET_SDK_IMAGE"
run_logged "$LOG_DIR/pull-dotnet-aspnet.log" docker pull "$DOTNET_ASPNET_IMAGE"
run_logged "$LOG_DIR/pull-nginx.log" docker pull "$NGINX_IMAGE"

build_image "$BASELINE_DIR" "BlazorShop.Presentation/BlazorShop.API/Dockerfile" "$BASELINE_API_IMAGE" "build-baseline-api.log"
build_image "$BASELINE_DIR" "BlazorShop.Presentation/BlazorShop.Storefront/Dockerfile" "$BASELINE_STOREFRONT_IMAGE" "build-baseline-storefront.log"
build_image "$BASELINE_DIR" "BlazorShop.Presentation/BlazorShop.Web/Dockerfile" "$BASELINE_WEB_IMAGE" "build-baseline-web.log"
build_image "$TARGET_DIR" "BlazorShop.Presentation/BlazorShop.API/Dockerfile" "$TARGET_API_IMAGE" "build-target-api.log"
build_image "$TARGET_DIR" "BlazorShop.Presentation/BlazorShop.Storefront/Dockerfile" "$TARGET_STOREFRONT_IMAGE" "build-target-storefront.log"
build_image "$TARGET_DIR" "BlazorShop.Presentation/BlazorShop.Web/Dockerfile" "$TARGET_WEB_IMAGE" "build-target-web.log"

{
    echo "tooling_sha=$(git -C "$TOOLING_DIR" rev-parse HEAD)"
    echo "baseline_sha=$baseline_actual"
    echo "target_sha=$target_actual"
    echo "postgres_image=$POSTGRES_IMAGE"
    echo "postgres_local_image_id=$(docker image inspect "$POSTGRES_IMAGE" --format '{{.Id}}')"
    echo "dotnet_sdk_base=$DOTNET_SDK_IMAGE"
    echo "dotnet_aspnet_base=$DOTNET_ASPNET_IMAGE"
    echo "nginx_base=$NGINX_IMAGE"
    echo "dotnet_sdk_local_image_id=$(docker image inspect "$DOTNET_SDK_IMAGE" --format '{{.Id}}')"
    echo "dotnet_aspnet_local_image_id=$(docker image inspect "$DOTNET_ASPNET_IMAGE" --format '{{.Id}}')"
    echo "nginx_local_image_id=$(docker image inspect "$NGINX_IMAGE" --format '{{.Id}}')"
    for image in \
        "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE" \
        "$TARGET_API_IMAGE" "$TARGET_STOREFRONT_IMAGE" "$TARGET_WEB_IMAGE"; do
        echo "$image=$(docker image inspect "$image" --format '{{.Id}}')"
    done
    echo "postgres_version=$(docker run --rm "$POSTGRES_IMAGE" postgres --version)"
    echo "baseline_api_runtimes=$(docker run --rm --entrypoint dotnet "$BASELINE_API_IMAGE" --list-runtimes | paste -sd ';' -)"
    echo "target_api_runtimes=$(docker run --rm --entrypoint dotnet "$TARGET_API_IMAGE" --list-runtimes | paste -sd ';' -)"
    echo "nginx_version=$(docker run --rm --entrypoint nginx "$TARGET_WEB_IMAGE" -v 2>&1)"
    echo "playwright_package=1.61.0"
} > "$REPORT_DIR/versions-and-images.txt"

dc up --detach postgres api
wait_postgres postgres
wait_http "$REHEARSAL_API_URL/health" "initial baseline API"
register_user
readonly USER_ID="$(scalar postgres "SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Email\" = '$USER_EMAIL';")"
if [[ -z "$USER_ID" ]]; then
    echo "Historical user ID was not persisted." >&2
    exit 1
fi

psql_service postgres --set "user_id=$USER_ID" < "$TOOLING_DIR/rehearsal/seed-baseline.sql" \
    > "$LOG_DIR/seed-baseline.log" 2>&1
dc up --detach storefront web
verify_baseline_application "pre-upgrade"
verify_baseline_database postgres
capture_baseline_report postgres "$REPORT_DIR/pre-upgrade-control.txt"
sha256sum "$REPORT_DIR/pre-upgrade-control.txt" > "$REPORT_DIR/pre-upgrade-control.sha256"
capture_logs "baseline"
stop_application_writers

docker compose --project-name "$PROJECT_NAME" --file "$COMPOSE_FILE" exec -T postgres \
    pg_dump --username "$REHEARSAL_DB_USER" --dbname "$REHEARSAL_DB_NAME" \
    --format custom --no-owner --no-acl > "$BACKUP_PATH"
if [[ ! -s "$BACKUP_PATH" ]]; then
    echo "pg_dump produced an empty backup." >&2
    exit 1
fi
docker compose --project-name "$PROJECT_NAME" --file "$COMPOSE_FILE" exec -T postgres \
    pg_restore --list < "$BACKUP_PATH" > "$REPORT_DIR/backup-contents.txt"
for required_object in CheckoutOrderItems Orders OrderLines ProductVariants; do
    if ! grep --fixed-strings --quiet "$required_object" "$REPORT_DIR/backup-contents.txt"; then
        echo "Backup archive does not list required object $required_object." >&2
        exit 1
    fi
done
{
    echo "format=PostgreSQL custom"
    echo "size_bytes=$(stat --format '%s' "$BACKUP_PATH")"
    echo "sha256=$(sha256sum "$BACKUP_PATH" | awk '{print $1}')"
    echo "pg_dump_exit=0"
    echo "pg_restore_list_exit=0"
} > "$REPORT_DIR/backup-metadata.txt"

export REHEARSAL_API_IMAGE="$TARGET_API_IMAGE"
export REHEARSAL_STOREFRONT_IMAGE="$TARGET_STOREFRONT_IMAGE"
export REHEARSAL_WEB_IMAGE="$TARGET_WEB_IMAGE"
export REHEARSAL_DB_HOST="postgres"

upgrade_started="$(date +%s%3N)"
dc up --detach --force-recreate api
wait_http "$REHEARSAL_API_URL/health" "target API after migration"
UPGRADE_MILLISECONDS="$(( $(date +%s%3N) - upgrade_started ))"
dc up --detach --force-recreate storefront web
wait_http "$REHEARSAL_STOREFRONT_URL/product/rehearsal-canvas-bag" "target Storefront"
wait_http "$REHEARSAL_WEB_URL/authentication/login/account" "target Web"

verify_target_database
capture_target_report
capture_logs "target-upgraded"

export BLAZORSHOP_REHEARSAL_MODE="attach-upgraded-database"
export BLAZORSHOP_REHEARSAL_TARGET_SHA="$TARGET_SHA"
export BLAZORSHOP_REHEARSAL_MARKER="$CATEGORY_ID"
export BLAZORSHOP_REHEARSAL_STOREFRONT_URL="$REHEARSAL_STOREFRONT_URL"
export BLAZORSHOP_REHEARSAL_WEB_URL="$REHEARSAL_WEB_URL"
export BLAZORSHOP_REHEARSAL_USER_EMAIL="$USER_EMAIL"
export BLAZORSHOP_REHEARSAL_USER_PASSWORD="$USER_PASSWORD"
export BLAZORSHOP_REHEARSAL_ARTIFACTS_DIR="$TEST_RESULTS_DIR/diagnostics"

run_logged "$LOG_DIR/browser-test.log" dotnet test \
    "$TOOLING_DIR/BlazorShop.StagingRehearsal/BlazorShop.StagingRehearsal.csproj" \
    --configuration Release --no-build --no-restore \
    --logger "trx;LogFileName=staging-rehearsal.trx" \
    --results-directory "$TEST_RESULTS_DIR" --verbosity normal
pwsh "$TOOLING_DIR/scripts/verify-staging-rehearsal-results.ps1" \
    -Path "$TEST_RESULTS_DIR/staging-rehearsal.trx"

capture_logs "target-after-browser"
stop_application_writers

dc --profile restore up --detach restore-postgres
wait_postgres restore-postgres
restore_started="$(date +%s%3N)"
dc --profile restore exec -T restore-postgres \
    pg_restore --username "$REHEARSAL_DB_USER" --dbname "$REHEARSAL_DB_NAME" \
    --exit-on-error --no-owner --no-acl < "$BACKUP_PATH" \
    > "$LOG_DIR/restore.log" 2>&1
RESTORE_MILLISECONDS="$(( $(date +%s%3N) - restore_started ))"

verify_baseline_database restore-postgres
capture_baseline_report restore-postgres "$REPORT_DIR/post-restore-control.txt"
cmp --silent "$REPORT_DIR/pre-upgrade-control.txt" "$REPORT_DIR/post-restore-control.txt"
sha256sum "$REPORT_DIR/post-restore-control.txt" > "$REPORT_DIR/post-restore-control.sha256"
assert_equal \
    "$(cut --delimiter ' ' --fields 1 "$REPORT_DIR/pre-upgrade-control.sha256")" \
    "$(cut --delimiter ' ' --fields 1 "$REPORT_DIR/post-restore-control.sha256")" \
    "restored baseline control checksum"

export REHEARSAL_API_IMAGE="$BASELINE_API_IMAGE"
export REHEARSAL_STOREFRONT_IMAGE="$BASELINE_STOREFRONT_IMAGE"
export REHEARSAL_WEB_IMAGE="$BASELINE_WEB_IMAGE"
export REHEARSAL_DB_HOST="restore-postgres"
dc --profile restore up --detach --force-recreate api storefront web
verify_baseline_application "post-restore"
capture_logs "restored-baseline"

REHEARSAL_RESULT="passed"
echo "PASS: hosted staging upgrade, browser, backup restore, and restored baseline application rehearsal"
