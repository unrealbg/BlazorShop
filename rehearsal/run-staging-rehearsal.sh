#!/usr/bin/env bash
set -Eeuo pipefail

# The target is pinned here, in the workflow checkout and in the attach-only browser suite.
# The baseline comes from a reviewed profile in rehearsal/baselines/ (see profile_value below).
readonly TARGET_SHA="527ee48056ffd01821e8d3f398c2a2be3fae522e"
readonly TARGET_MIGRATION_COUNT="13"
readonly TARGET_MIGRATION_HEAD="20261101131138_AddRegistrationConfirmationOutbox"
readonly BASELINE_MIGRATIONS=(
    20260412183234_InitialCreate
    20260412211723_AlignIdentityKeyLengths
    20260414195649_ProductionHardeningPass
    20260418101205_SeoFoundation
    20260424183749_AdminBackOfficeMvp
)
readonly UPGRADE_MIGRATIONS=(
    20260820134400_PreserveCheckoutProductVariants
    20260820145506_AddOrderLinePurchaseSnapshots
    20260820152625_AddInventoryReservations
    20260820204600_AddCheckoutIdempotency
    20260821084312_AddPaymentReconciliation
    20260908215523_SeparateOrderPaymentAndFulfillmentStatuses
    20260909075202_ArchiveLegacyCheckoutOrderItems
    20261101131138_AddRegistrationConfirmationOutbox
)
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
if [[ "${GITHUB_ACTIONS:-}" != true || "${RUNNER_ENVIRONMENT:-}" != github-hosted || "${RUNNER_OS:-}" != Linux ]]; then
    echo "This rehearsal requires a GitHub-hosted Linux runner." >&2
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
readonly PORT_SEED="$(printf '%s' "${GITHUB_RUN_ID:-0}" | tr -cd '0-9' | tail -c 4)"
readonly PORT_OFFSET="$((10#${PORT_SEED:-0} % 15000))"
readonly RUNTIME_DIR="$(mktemp -d "${RUNNER_TEMP:-/tmp}/blazorshop-rehearsal.XXXXXX")"
readonly BACKUP_PATH="$RUNTIME_DIR/baseline.dump"
readonly PROTECTED_RESPONSE_PATH="$RUNTIME_DIR/protected-response.json"
# Volume archives, key-ring manifests and the protected probe payload stay in RUNTIME_DIR, which
# is deleted on exit and never uploaded. Reports contain only counts, checksums and results.
readonly VOLUME_WORK_DIR="$RUNTIME_DIR/volumes"
readonly PROBE_DIR="$RUNTIME_DIR/data-protection-probe"
readonly UPLOAD_FIXTURE_DIR="rehearsal-fixture"
readonly UPLOAD_FIXTURES=("pixel.png" "random.png")

mkdir -p "$LOG_DIR" "$REPORT_DIR" "$TEST_RESULTS_DIR" "$VOLUME_WORK_DIR" "$PROBE_DIR"
: > "$REPORT_DIR/assertions.txt"

# Both reviewed profiles use the same five-migration synthetic fixture.
readonly BASELINE_PROFILE="${REHEARSAL_BASELINE_PROFILE:-historical}"
if [[ ! "$BASELINE_PROFILE" =~ ^[a-z0-9-]+$ ]]; then
    echo "Invalid baseline profile name '$BASELINE_PROFILE'." >&2
    exit 2
fi
readonly PROJECT_NAME="bsrehearsal${RUN_NUMBER//[^0-9]/0}a${RUN_ATTEMPT//[^0-9]/0}-${BASELINE_PROFILE}"
readonly BASELINE_PROFILE_FILE="$TOOLING_DIR/rehearsal/baselines/$BASELINE_PROFILE.env"
if [[ ! -f "$BASELINE_PROFILE_FILE" ]]; then
    echo "Baseline profile '$BASELINE_PROFILE' is BLOCKED: no reviewed profile at rehearsal/baselines/$BASELINE_PROFILE.env." >&2
    exit 2
fi

profile_value() {
    local key="$1"
    local pattern="$2"
    local value
    value="$(sed -n "s/^${key}=//p" "$BASELINE_PROFILE_FILE" | tr -d '\r')"
    if [[ ! "$value" =~ $pattern ]]; then
        echo "Baseline profile '$BASELINE_PROFILE' has an invalid or missing $key." >&2
        return 1
    fi
    printf '%s' "$value"
}

BASELINE_SHA="$(profile_value BASELINE_SHA '^[0-9a-f]{40}$')"
BASELINE_MIGRATION_COUNT="$(profile_value BASELINE_MIGRATION_COUNT '^[1-9][0-9]*$')"
BASELINE_MIGRATION_HEAD="$(profile_value BASELINE_MIGRATION_HEAD '^[0-9]{14}_[A-Za-z0-9]+$')"
BASELINE_FIXTURE="$(profile_value BASELINE_FIXTURE '^[a-z0-9-]+$')"
readonly BASELINE_SHA BASELINE_MIGRATION_COUNT BASELINE_MIGRATION_HEAD BASELINE_FIXTURE
if [[ "$BASELINE_FIXTURE" != "historical-5" ]]; then
    echo "Baseline fixture '$BASELINE_FIXTURE' is not implemented; only the historical-5 seed and assertions exist." >&2
    exit 2
fi

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
export REHEARSAL_SMTP_PASSWORD="$(openssl rand -hex 32)"
export REHEARSAL_API_PORT="$((21000 + PORT_OFFSET))"
export REHEARSAL_STOREFRONT_PORT="$((REHEARSAL_API_PORT + 1))"
export REHEARSAL_WEB_PORT="$((REHEARSAL_API_PORT + 2))"
export REHEARSAL_MAIL_PORT="$((REHEARSAL_API_PORT + 3))"
export REHEARSAL_API_URL="http://localhost:$REHEARSAL_API_PORT"
export REHEARSAL_STOREFRONT_URL="http://localhost:$REHEARSAL_STOREFRONT_PORT"
export REHEARSAL_WEB_URL="http://localhost:$REHEARSAL_WEB_PORT"
export REHEARSAL_DB_HOST="postgres"
export REHEARSAL_UPLOADS_VOLUME="uploads-data"
export REHEARSAL_DATA_PROTECTION_VOLUME="data-protection-keys"
export REHEARSAL_PROBE_IMAGE="$DOTNET_ASPNET_IMAGE"
export REHEARSAL_PROBE_DIR="$PROBE_DIR"

readonly USER_EMAIL="rehearsal.user.${RUN_NUMBER}.${RUN_ATTEMPT}@example.test"
readonly USER_PASSWORD="$(generate_user_password)"
readonly ADMIN_EMAIL="rehearsal.admin.${RUN_NUMBER}.${RUN_ATTEMPT}@example.test"
readonly ADMIN_PASSWORD="$(generate_user_password)"
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

BACKUP_DATABASE_MILLISECONDS="not-run"
BACKUP_VOLUMES_MILLISECONDS="not-run"
UPGRADE_MILLISECONDS="not-run"
RESTORE_MILLISECONDS="not-run"
RESTORE_VOLUMES_MILLISECONDS="not-run"
MIGRATION_HEAD_BEFORE="not-read"
MIGRATION_HEAD_AFTER="not-read"
UPLOADS_RESULT="not-run"
DATA_PROTECTION_RESULT="not-run"
VOLUME_RESTORE_RESULT="not-run"
BROWSER_RESULT="not-run"
BASELINE_BROWSER_RESULT="not-run"
FAILURE_CONTEXT=""
REHEARSAL_RESULT="failed"
CAPTURE_API_LOGS=true

# Record the first failing command (unexpanded, so no generated secret is written) for the report.
trap 'if [[ -z "$FAILURE_CONTEXT" ]]; then FAILURE_CONTEXT="line $LINENO: $BASH_COMMAND"; fi' ERR

dc() {
    docker compose --project-name "$PROJECT_NAME" --file "$COMPOSE_FILE" "$@"
}

capture_logs() {
    local phase="$1"
    local services=(postgres restore-postgres storefront web)
    if [[ "$CAPTURE_API_LOGS" == true ]]; then services+=(api); fi
    if ! dc --profile restore --profile tools --profile mail logs --no-color --timestamps "${services[@]}" > "$LOG_DIR/${phase}-compose.log" 2>&1; then
        echo "Unable to capture complete Compose logs for phase $phase." >> "$LOG_DIR/${phase}-compose.log"
    fi
    dc --profile restore --profile tools --profile mail ps --all > "$LOG_DIR/${phase}-compose-ps.txt" 2>&1 || :
}

cleanup() {
    local original_exit="$?"
    trap - EXIT ERR
    set +e

    capture_logs "final"
    local cleanup_started
    cleanup_started="$(date +%s%3N)"
    dc --profile restore --profile tools --profile mail down --volumes --remove-orphans --timeout 20 > "$LOG_DIR/cleanup.log" 2>&1
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
        REHEARSAL_RESULT=failed
    fi

    {
        echo "result=$REHEARSAL_RESULT"
        echo "baseline_profile=$BASELINE_PROFILE"
        echo "baseline_sha=$BASELINE_SHA"
        echo "target_sha=$TARGET_SHA"
        echo "migration_head_before_upgrade=$MIGRATION_HEAD_BEFORE"
        echo "migration_head_after_upgrade=$MIGRATION_HEAD_AFTER"
        echo "backup_database_duration_milliseconds=$BACKUP_DATABASE_MILLISECONDS"
        echo "backup_volumes_duration_milliseconds=$BACKUP_VOLUMES_MILLISECONDS"
        echo "upgrade_duration_milliseconds=$UPGRADE_MILLISECONDS"
        echo "restore_duration_milliseconds=$RESTORE_MILLISECONDS"
        echo "restore_volumes_duration_milliseconds=$RESTORE_VOLUMES_MILLISECONDS"
        echo "cleanup_duration_milliseconds=$cleanup_milliseconds"
        echo "uploads_fixture=$UPLOADS_RESULT"
        echo "data_protection_probe=$DATA_PROTECTION_RESULT"
        echo "volume_restore=$VOLUME_RESTORE_RESULT"
        echo "browser_tests=$BROWSER_RESULT"
        echo "baseline_browser_tests=$BASELINE_BROWSER_RESULT"
        echo "assertions_passed=$(grep -c '^PASS:' "$REPORT_DIR/assertions.txt" || :)"
        echo "assertions_failed=$(grep -c '^FAIL:' "$REPORT_DIR/assertions.txt" || :)"
        echo "failure_context=${FAILURE_CONTEXT:-none}"
        echo "note=synthetic isolated rehearsal; durations are not VPS RPO/RTO"
    } > "$REPORT_DIR/result.txt"

    exit "$original_exit"
}
trap cleanup EXIT

run_logged() {
    local log_path="$1"
    shift
    if "$@" 2>&1 | tee "$log_path"; then
        return 0
    else
        local exit_code=$?
        FAILURE_CONTEXT="${FAILURE_CONTEXT:-logged operation failed; see $(basename "$log_path")}"
        echo "Logged operation failed; see $log_path." >&2
        return "$exit_code"
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
        echo "FAIL: $description: expected '$expected', received '$actual'" >> "$REPORT_DIR/assertions.txt"
        return 1
    fi
    echo "PASS: $description"
    echo "PASS: $description" >> "$REPORT_DIR/assertions.txt"
}

scalar_stdin() {
    local service="$1"
    psql_service "$service" --quiet --tuples-only --no-align | tr -d '\r' | sed '/^[[:space:]]*$/d'
}

table_fingerprints() {
    local service="$1"
    scalar_stdin "$service" <<'SQL'
SELECT format(
    'SELECT %L, count(*), encode(sha256(convert_to(coalesce(string_agg(row_text, chr(10) ORDER BY row_text COLLATE "C"), ''''), ''UTF8'')), ''hex'') FROM (SELECT to_jsonb(t)::text AS row_text FROM %I.%I t) snapshot',
    tablename, schemaname, tablename)
FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename COLLATE "C"
\gexec
SQL
}

migration_ids() {
    scalar "$1" 'SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C";'
}

# Exact row counts for every table in the public schema, sorted with the C collation.
table_counts() {
    scalar_stdin "$1" <<'SQL'
SELECT format('%s=%s', c.relname,
    (xpath('/row/n/text()', query_to_xml(format('SELECT count(*) AS n FROM %I.%I', ns.nspname, c.relname), false, true, '')))[1]::text)
FROM pg_class c
JOIN pg_namespace ns ON ns.oid = c.relnamespace
WHERE ns.nspname = 'public' AND c.relkind IN ('r', 'p')
ORDER BY c.relname COLLATE "C";
SQL
}

# Every pre-upgrade table keeps its exact row count (CheckoutOrderItems is renamed to the archive)
# and every table the upgrade adds is empty. Only the migration history may grow.
assert_upgrade_preserves_rows() {
    local before="$1"
    local after="$2"
    local violations
    violations="$(awk -F= '
        NR == FNR {
            if ($1 != "__EFMigrationsHistory") {
                name = ($1 == "CheckoutOrderItems") ? "LegacyCheckoutOrderItemsArchive" : $1
                expected[name] = $2
            }
            next
        }
        $1 == "__EFMigrationsHistory" { next }
        ($1 in expected) {
            seen[$1] = 1
            if ($2 != expected[$1]) print "changed " $1 ": " expected[$1] " -> " $2
            next
        }
        $2 != 0 { print "rows in new table " $1 ": " $2 }
        END { for (name in expected) if (!(name in seen)) print "missing table " name }
    ' "$before" "$after" | sort)"
    assert_equal "" "$violations" "upgrade preserves every table row count and creates no rows in new tables"
}

capture_baseline_report() {
    local service="$1"
    local output="$2"
    {
        echo "application_sha=$BASELINE_SHA"
        echo "schema=$BASELINE_FIXTURE"
        echo "table_row_counts"
        table_counts "$service"
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
    assert_equal "$(printf '%s\n' "${BASELINE_MIGRATIONS[@]}")" "$(migration_ids "$service")" "exact five baseline migration IDs ($service)"
    assert_equal "$BASELINE_MIGRATION_COUNT" "$(scalar "$service" 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" "baseline migration count"
    assert_equal "$BASELINE_MIGRATION_HEAD" "$(scalar "$service" 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')" "baseline migration head"
    assert_equal "Rehearsal Footwear|rehearsal-footwear|t" "$(scalar "$service" "SELECT \"Name\", \"Slug\", \"IsPublished\" FROM \"Categories\" WHERE \"Id\" = '$CATEGORY_ID';")" "baseline catalog marker"
    assert_equal "19|0|7" "$(scalar "$service" "SELECT (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$SIMPLE_PRODUCT_ID'), (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$VARIANT_PRODUCT_ID'), (SELECT \"Stock\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID');")" "baseline inventory values"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order count"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order-line count"
    assert_equal "Paid|delivered|25.00|Pending|OutForDelivery|62.50" "$(scalar "$service" "SELECT string_agg(\"Status\" || '|' || \"ShippingStatus\" || '|' || to_char(\"TotalAmount\", 'FM999999990.00'), '|' ORDER BY \"Id\") FROM \"Orders\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order lifecycle and totals"
    assert_equal "87.50" "$(scalar "$service" "SELECT to_char(SUM(\"UnitPrice\" * \"Quantity\"), 'FM999999990.00') FROM \"OrderLines\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline order-line total"
    assert_equal "2" "$(scalar "$service" "SELECT COUNT(*) FROM \"CheckoutOrderItems\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline checkout-history count"
    assert_equal "4" "$(scalar "$service" "SELECT SUM(\"Quantity\") FROM \"CheckoutOrderItems\" WHERE \"Id\"::text LIKE '37000000-0000-0000-0000-000000000%';")" "baseline checkout-history quantities"
}

verify_registration_outbox_schema() {
    assert_equal "yes" "$(scalar postgres "SELECT CASE WHEN to_regclass('\"RegistrationConfirmationDeliveries\"') IS NOT NULL THEN 'yes' ELSE 'no' END;")" "registration outbox table exists"
    assert_equal \
        "Id:uuid:-:NO,UserId:character varying:450:NO,EventId:uuid:-:NO,EmailSnapshot:character varying:256:NO,SecurityStampHash:character varying:64:NO,Generation:integer:-:NO,State:integer:-:NO,AttemptCount:integer:-:NO,CreatedOn:timestamp with time zone:-:NO,UpdatedOn:timestamp with time zone:-:NO,LastRequestedOn:timestamp with time zone:-:NO,RequestWindowStartedOn:timestamp with time zone:-:NO,RequestsInWindow:integer:-:NO,NextAttemptOn:timestamp with time zone:-:NO,LastAttemptOn:timestamp with time zone:-:YES,DeliveredOn:timestamp with time zone:-:YES,LeaseOwnerId:uuid:-:YES,LeaseExpiresOn:timestamp with time zone:-:YES,LastFailureCode:character varying:64:YES" \
        "$(scalar_stdin postgres <<'SQL'
SELECT string_agg(format('%s:%s:%s:%s', column_name, data_type, COALESCE(character_maximum_length::text, '-'), is_nullable), ',' ORDER BY ordinal_position)
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'RegistrationConfirmationDeliveries';
SQL
)" "registration outbox columns, types, lengths and nullability"
    assert_equal \
        "IX_RegistrationConfirmationDeliveries_State_LeaseExpiresOn|non-unique|State,LeaseExpiresOn;IX_RegistrationConfirmationDeliveries_State_NextAttemptOn|non-unique|State,NextAttemptOn;IX_RegistrationConfirmationDeliveries_UserId|unique|UserId;PK_RegistrationConfirmationDeliveries|unique|Id" \
        "$(scalar_stdin postgres <<'SQL'
SELECT string_agg(format('%s|%s|%s', i.relname,
        CASE WHEN x.indisunique THEN 'unique' ELSE 'non-unique' END,
        (SELECT string_agg(a.attname, ',' ORDER BY k.ord)
         FROM unnest(x.indkey::int2[]) WITH ORDINALITY AS k(attnum, ord)
         JOIN pg_attribute a ON a.attrelid = x.indrelid AND a.attnum = k.attnum)),
    ';' ORDER BY i.relname COLLATE "C")
FROM pg_index x
JOIN pg_class i ON i.oid = x.indexrelid
WHERE x.indrelid = '"RegistrationConfirmationDeliveries"'::regclass;
SQL
)" "registration outbox primary key and indexes"
    assert_equal \
        'FK_RegistrationConfirmationDeliveries_AspNetUsers_UserId|"AspNetUsers"|c|FOREIGN KEY ("UserId") REFERENCES "AspNetUsers"("Id") ON DELETE CASCADE' \
        "$(scalar_stdin postgres <<'SQL'
SELECT string_agg(format('%s|%s|%s|%s', c.conname, c.confrelid::regclass::text, c.confdeltype, pg_get_constraintdef(c.oid)), ';' ORDER BY c.conname COLLATE "C")
FROM pg_constraint c
WHERE c.conrelid = '"RegistrationConfirmationDeliveries"'::regclass AND c.contype = 'f';
SQL
)" "registration outbox foreign key to AspNetUsers with cascade delete"
    assert_equal "CK_ConfirmationDelivery_Counts,CK_ConfirmationDelivery_State" \
        "$(scalar postgres "SELECT string_agg(conname, ',' ORDER BY conname COLLATE \"C\") FROM pg_constraint WHERE conrelid = '\"RegistrationConfirmationDeliveries\"'::regclass AND contype = 'c';")" \
        "registration outbox check constraints"
    assert_equal "0" "$(scalar postgres 'SELECT COUNT(*) FROM "RegistrationConfirmationDeliveries";')" "no outbox rows invented for existing users"

    # Behavioural cascade check inside a transaction that is always rolled back: a synthetic user
    # with one outbox row is deleted and the row must go with it. Nothing is committed.
    assert_equal "1|0" "$(scalar_stdin postgres <<'SQL' | paste -sd '|' -
BEGIN;
INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed", "FullName",
    "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount", "RequirePasswordChange", "CreatedOn")
VALUES ('rehearsal-cascade-probe', 'cascade.probe@example.test', 'CASCADE.PROBE@EXAMPLE.TEST', 'cascade.probe@example.test',
    'CASCADE.PROBE@EXAMPLE.TEST', false, 'Cascade Probe', false, false, false, 0, false, now());
INSERT INTO "RegistrationConfirmationDeliveries" ("Id", "UserId", "EventId", "EmailSnapshot", "SecurityStampHash", "Generation", "State",
    "AttemptCount", "CreatedOn", "UpdatedOn", "LastRequestedOn", "RequestWindowStartedOn", "RequestsInWindow", "NextAttemptOn")
VALUES (gen_random_uuid(), 'rehearsal-cascade-probe', gen_random_uuid(), 'cascade.probe@example.test', repeat('0', 64), 1, 0,
    0, now(), now(), now(), now(), 1, now());
SELECT COUNT(*) FROM "RegistrationConfirmationDeliveries" WHERE "UserId" = 'rehearsal-cascade-probe';
DELETE FROM "AspNetUsers" WHERE "Id" = 'rehearsal-cascade-probe';
SELECT COUNT(*) FROM "RegistrationConfirmationDeliveries" WHERE "UserId" = 'rehearsal-cascade-probe';
ROLLBACK;
SQL
)" "deleting a user cascades to its outbox row (rolled back)"
    assert_equal "0|0" "$(scalar postgres "SELECT (SELECT COUNT(*) FROM \"AspNetUsers\" WHERE \"Id\" = 'rehearsal-cascade-probe'), (SELECT COUNT(*) FROM \"RegistrationConfirmationDeliveries\");")" "cascade check left no rows behind"
}

verify_target_database() {
    assert_equal "$(printf '%s\n' "${BASELINE_MIGRATIONS[@]}" "${UPGRADE_MIGRATIONS[@]}")" "$(migration_ids postgres)" "exact eight forward migrations appended"
    assert_equal "$TARGET_MIGRATION_COUNT" "$(scalar postgres 'SELECT COUNT(*) FROM "__EFMigrationsHistory";')" "target migration count"
    assert_equal "$TARGET_MIGRATION_HEAD" "$(scalar postgres 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')" "target migration head"
    table_counts postgres > "$REPORT_DIR/table-counts-post-upgrade.txt"
    assert_upgrade_preserves_rows "$REPORT_DIR/table-counts-pre-upgrade.txt" "$REPORT_DIR/table-counts-post-upgrade.txt"
    verify_registration_outbox_schema
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
        echo "registration_outbox_count"
        scalar postgres 'SELECT COUNT(*) FROM "RegistrationConfirmationDeliveries";'
        echo "table_row_counts"
        cat "$REPORT_DIR/table-counts-post-upgrade.txt"
    } > "$REPORT_DIR/post-upgrade-control.txt"
}

# Schema-only dump without the per-run random \restrict key, so two dumps of one schema compare equal.
schema_dump() {
    dc --profile restore exec -T "$1" \
        pg_dump --username "$REHEARSAL_DB_USER" --dbname "$REHEARSAL_DB_NAME" --schema-only --no-owner --no-acl \
        | tr -d '\r' | grep -v -E '^\\(un)?restrict '
}

volume_exec() {
    dc --profile tools exec -T volume-tool sh -euc "$1"
}

probe_exec() {
    dc --profile tools exec -T data-protection-probe dotnet /probe/BlazorShop.DataProtectionProbe.dll "$@"
}

# Sorted "sha256  ./path" lines for every regular file in one volume of this Compose project.
volume_manifest() {
    volume_exec "cd '/volumes/$1/$2' && find . -type f | LC_ALL=C sort | while IFS= read -r file; do sha256sum \"\$file\"; done"
}

assert_volume_empty() {
    assert_equal "0" "$(volume_exec "find '/volumes/$1/$2' -mindepth 1 | wc -l" | tr -d ' ')" "$1 $2 volume is empty before use"
}

seed_upload_fixtures() {
    # A valid 1x1 PNG and 64 KiB of random bytes, both synthetic.
    printf '%s' 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==' \
        | base64 --decode > "$VOLUME_WORK_DIR/pixel.png"
    openssl rand 65536 > "$VOLUME_WORK_DIR/random.png"
    volume_exec "mkdir -p '/volumes/primary/uploads/$UPLOAD_FIXTURE_DIR'"
    local fixture
    for fixture in "${UPLOAD_FIXTURES[@]}"; do
        volume_exec "cat > '/volumes/primary/uploads/$UPLOAD_FIXTURE_DIR/$fixture'" < "$VOLUME_WORK_DIR/$fixture"
        sha256sum "$VOLUME_WORK_DIR/$fixture" | awk -v name="$fixture" '{print $1 "  " name}' >> "$VOLUME_WORK_DIR/fixtures.sha256"
    done
    cp "$VOLUME_WORK_DIR/fixtures.sha256" "$REPORT_DIR/upload-fixtures.sha256"
}

# Reads each synthetic upload through the API's public /uploads path and compares its SHA-256.
verify_upload_fixtures_served() {
    local phase="$1"
    local fixture expected actual
    for fixture in "${UPLOAD_FIXTURES[@]}"; do
        expected="$(awk -v name="$fixture" '$2 == name {print $1}' "$VOLUME_WORK_DIR/fixtures.sha256")"
        actual="$(curl --fail --silent --show-error "$REHEARSAL_API_URL/uploads/$UPLOAD_FIXTURE_DIR/$fixture" | sha256sum | awk '{print $1}')"
        assert_equal "$expected" "$actual" "$phase upload fixture $fixture is served unchanged"
    done
}

data_protection_unprotect() {
    local ring="$1"
    probe_exec unprotect "/keys/$ring" < "$VOLUME_WORK_DIR/data-protection-payload.txt"
}

assert_data_protection_round_trip() {
    local ring="$1"
    local phase="$2"
    local recovered
    recovered="$(data_protection_unprotect "$ring")"
    assert_equal "$(cat "$VOLUME_WORK_DIR/data-protection-plaintext.txt")" "$recovered" \
        "$phase key ring unprotects the value protected before backup"
}

backup_volume() {
    local name="$1"
    volume_exec "tar -C '/volumes/primary/$name' -czf - ." > "$VOLUME_WORK_DIR/$name.tar.gz"
    gzip --test "$VOLUME_WORK_DIR/$name.tar.gz"
    assert_equal \
        "$(wc -l < "$VOLUME_WORK_DIR/$name.pre-backup.manifest" | tr -d ' ')" \
        "$(tar -tvzf "$VOLUME_WORK_DIR/$name.tar.gz" | awk '$1 ~ /^-/' | wc -l | tr -d ' ')" \
        "$name archive is readable and lists every file"
}

restore_volume() {
    local name="$1"
    volume_exec "tar -C '/volumes/restore/$name' -xzf -" < "$VOLUME_WORK_DIR/$name.tar.gz"
    volume_manifest restore "$name" > "$VOLUME_WORK_DIR/$name.post-restore.manifest"
    local verdict="identical"
    cmp --silent "$VOLUME_WORK_DIR/$name.pre-backup.manifest" "$VOLUME_WORK_DIR/$name.post-restore.manifest" || verdict="different"
    assert_equal "identical" "$verdict" "$name restored content matches the pre-backup manifest byte for byte"
}

write_volume_report() {
    local name
    {
        for name in uploads data-protection-keys; do
            echo "${name}_files=$(wc -l < "$VOLUME_WORK_DIR/$name.pre-backup.manifest" | tr -d ' ')"
            echo "${name}_manifest_sha256=$(sha256sum "$VOLUME_WORK_DIR/$name.pre-backup.manifest" | awk '{print $1}')"
            echo "${name}_archive_sha256=$(sha256sum "$VOLUME_WORK_DIR/$name.tar.gz" | awk '{print $1}')"
            echo "${name}_archive_size_bytes=$(stat --format '%s' "$VOLUME_WORK_DIR/$name.tar.gz")"
        done
    } > "$REPORT_DIR/volume-backup-metadata.txt"
}

register_user() {
    local email="$1" password="$2" payload
    payload="$(jq --compact-output --null-input \
        --arg email "$email" \
        --arg password "$password" \
        '{fullName:"Rehearsal Customer",email:$email,password:$password,confirmPassword:$password}')"
    local status
    status="$(curl --silent --show-error --output "$RUNTIME_DIR/register-response.json" --write-out '%{http_code}' \
        --header 'Content-Type: application/json' --request POST --data "$payload" \
        "$REHEARSAL_API_URL/api/authentication/create")"
    assert_equal "200" "$status" "synthetic user registration ($email)"
    assert_equal "true" "$(jq --raw-output '.success' "$RUNTIME_DIR/register-response.json")" "synthetic registration succeeds ($email)"
}

# shellcheck source=rehearsal/rehearsal-application-checks.sh
source "$TOOLING_DIR/rehearsal/rehearsal-application-checks.sh"

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

    login_user "$USER_EMAIL" "$USER_PASSWORD" "$RUNTIME_DIR/customer-$phase" "$REHEARSAL_WEB_URL"
    local access_token
    access_token="$(jq --exit-status --raw-output '.token' "$RUNTIME_DIR/customer-$phase.json")"
    curl --fail --silent --show-error --max-time 15 --header "Authorization: Bearer $access_token" \
        "$REHEARSAL_WEB_URL/api/cart/user/orders" --output "$PROTECTED_RESPONSE_PATH"
    assert_equal true "$(jq -r 'map(select(.reference == "REHEARSAL-LEGACY-001")) | length == 1' "$PROTECTED_RESPONSE_PATH")" \
        "$phase protected order history through Web proxy"
    assert_equal 403 "$(curl -sS --max-time 15 -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $access_token" "$REHEARSAL_API_URL/api/cart/orders")" \
        "$phase customer cannot read all orders"
    login_user "$ADMIN_EMAIL" "$ADMIN_PASSWORD" "$RUNTIME_DIR/first-user-$phase"
    access_token="$(jq -er '.token' "$RUNTIME_DIR/first-user-$phase.json")"
    assert_equal "$FIRST_USER_ADMIN_HTTP" "$(curl -sS --max-time 15 -o /dev/null -w '%{http_code}' -H "Authorization: Bearer $access_token" "$REHEARSAL_API_URL/api/cart/orders")" \
        "$phase first user's effective admin access"
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

run_browser_scenario() {
    local phase="$1" test_class="$2"
    shift 2
    export BLAZORSHOP_REHEARSAL_MODE="attach-$phase-database"
    export BLAZORSHOP_REHEARSAL_TARGET_SHA="$TARGET_SHA"
    export BLAZORSHOP_REHEARSAL_BASELINE_SHA="$BASELINE_SHA"
    export BLAZORSHOP_REHEARSAL_MARKER="$CATEGORY_ID"
    export BLAZORSHOP_REHEARSAL_STOREFRONT_URL="$REHEARSAL_STOREFRONT_URL"
    export BLAZORSHOP_REHEARSAL_WEB_URL="$REHEARSAL_WEB_URL"
    export BLAZORSHOP_REHEARSAL_USER_EMAIL="$USER_EMAIL"
    export BLAZORSHOP_REHEARSAL_USER_PASSWORD="$USER_PASSWORD"
    export BLAZORSHOP_REHEARSAL_ARTIFACTS_DIR="$TEST_RESULTS_DIR/$phase-diagnostics"
    run_logged "$LOG_DIR/$phase-browser-test.log" dotnet test \
        "$TOOLING_DIR/BlazorShop.StagingRehearsal/BlazorShop.StagingRehearsal.csproj" \
        --configuration Release --no-build --no-restore --filter "FullyQualifiedName~$test_class" \
        --logger "trx;LogFileName=$phase-rehearsal.trx" --results-directory "$TEST_RESULTS_DIR" --verbosity normal
    pwsh "$TOOLING_DIR/scripts/verify-staging-rehearsal-results.ps1" -Path "$TEST_RESULTS_DIR/$phase-rehearsal.trx" "$@"
    assert_equal passed passed "$phase browser smoke"
}

baseline_actual="$(git -C "$BASELINE_DIR" rev-parse HEAD)"
target_actual="$(git -C "$TARGET_DIR" rev-parse HEAD)"
assert_equal "$BASELINE_SHA" "$baseline_actual" "baseline application source SHA"
assert_equal "$TARGET_SHA" "$target_actual" "target application source SHA"
assert_equal "" "$(git -C "$BASELINE_DIR" status --porcelain)" "clean baseline source checkout"
assert_equal "" "$(git -C "$TARGET_DIR" status --porcelain)" "clean target source checkout"

run_logged "$LOG_DIR/build-data-protection-probe.log" dotnet publish \
    "$TOOLING_DIR/rehearsal/data-protection-probe/BlazorShop.DataProtectionProbe.csproj" \
    --configuration Release --output "$PROBE_DIR" -warnaserror

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
dc --profile tools up --detach volume-tool data-protection-probe
assert_volume_empty restore uploads
assert_volume_empty restore data-protection-keys
wait_postgres postgres
wait_http "$REHEARSAL_API_URL/health" "initial baseline API"
readonly FIRST_USER_ROLE="$([[ "$BASELINE_PROFILE" == candidate-f40b870 ]] && echo Admin || echo User)"
readonly FIRST_USER_ADMIN_HTTP="$([[ "$FIRST_USER_ROLE" == Admin ]] && echo 200 || echo 403)"
assert_equal 0 "$(scalar postgres 'SELECT COUNT(*) FROM "AspNetUsers";')" "baseline begins with no Identity users"
register_user "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
verify_role "$ADMIN_EMAIL" "$FIRST_USER_ROLE"
register_user "$USER_EMAIL" "$USER_PASSWORD"
verify_role "$USER_EMAIL" User
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
BASELINE_BROWSER_RESULT="failed"
run_browser_scenario baseline BaselineBrowserTests -Baseline
BASELINE_BROWSER_RESULT="passed"
check_pre_upgrade_sessions
PRE_UPGRADE_IDENTITY="$(identity_fingerprint postgres)"
readonly PRE_UPGRADE_IDENTITY
MIGRATION_HEAD_BEFORE="$(scalar postgres 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')"

# Synthetic persistent-volume data: two uploads served by the baseline API and one value protected
# with a Data Protection key ring that lives in the API's key volume.
UPLOADS_RESULT="failed"
DATA_PROTECTION_RESULT="failed"
seed_upload_fixtures
verify_upload_fixtures_served "pre-upgrade baseline"
openssl rand -hex 32 | tr -d '\n' > "$VOLUME_WORK_DIR/data-protection-plaintext.txt"
probe_exec protect /keys/primary < "$VOLUME_WORK_DIR/data-protection-plaintext.txt" \
    > "$VOLUME_WORK_DIR/data-protection-payload.txt"
assert_equal "yes" "$([[ -s "$VOLUME_WORK_DIR/data-protection-payload.txt" ]] && echo yes || echo no)" "synthetic value protected before backup"
assert_data_protection_round_trip primary "pre-backup"

capture_baseline_report postgres "$REPORT_DIR/pre-upgrade-control.txt"
sha256sum "$REPORT_DIR/pre-upgrade-control.txt" > "$REPORT_DIR/pre-upgrade-control.sha256"
table_counts postgres > "$REPORT_DIR/table-counts-pre-upgrade.txt"
capture_logs "baseline"
stop_application_writers
schema_dump postgres > "$REPORT_DIR/schema-pre-upgrade.sql"
table_fingerprints postgres > "$REPORT_DIR/rows-pre-upgrade.txt"

backup_started="$(date +%s%3N)"
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
BACKUP_DATABASE_MILLISECONDS="$(( $(date +%s%3N) - backup_started ))"

volumes_backup_started="$(date +%s%3N)"
volume_manifest primary uploads > "$VOLUME_WORK_DIR/uploads.pre-backup.manifest"
volume_manifest primary data-protection-keys > "$VOLUME_WORK_DIR/data-protection-keys.pre-backup.manifest"
assert_equal "${#UPLOAD_FIXTURES[@]}" "$(wc -l < "$VOLUME_WORK_DIR/uploads.pre-backup.manifest" | tr -d ' ')" "uploads volume holds exactly the synthetic fixtures"
assert_equal "yes" "$([[ "$(wc -l < "$VOLUME_WORK_DIR/data-protection-keys.pre-backup.manifest")" -ge 1 ]] && echo yes || echo no)" "data-protection key volume holds at least one key"
backup_volume uploads
backup_volume data-protection-keys
BACKUP_VOLUMES_MILLISECONDS="$(( $(date +%s%3N) - volumes_backup_started ))"
write_volume_report
cp "$VOLUME_WORK_DIR/uploads.pre-backup.manifest" "$REPORT_DIR/uploads.pre-backup.manifest"

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
assert_equal "$PRE_UPGRADE_IDENTITY" "$(identity_fingerprint postgres)" "upgrade preserves user IDs, roles, confirmation and password hashes"
verify_baseline_application "post-upgrade"
check_upgraded_sessions "$(session_cookie "$RUNTIME_DIR/baseline-survivor")"
MIGRATION_HEAD_AFTER="$(scalar postgres 'SELECT MAX("MigrationId") FROM "__EFMigrationsHistory";')"
capture_target_report

# The upgrade must keep every pre-upgrade upload and key unchanged and still unprotect the value.
verify_upload_fixtures_served "post-upgrade target"
volume_manifest primary uploads > "$VOLUME_WORK_DIR/uploads.post-upgrade.manifest"
uploads_verdict="identical"
cmp --silent "$VOLUME_WORK_DIR/uploads.pre-backup.manifest" "$VOLUME_WORK_DIR/uploads.post-upgrade.manifest" || uploads_verdict="different"
assert_equal "identical" "$uploads_verdict" "upgrade leaves the uploads volume unchanged"
volume_manifest primary data-protection-keys > "$VOLUME_WORK_DIR/data-protection-keys.post-upgrade.manifest"
# Pre-upgrade manifest lines absent after the upgrade. comm exits 0 when there are none; grep -v
# would exit 1 on that success path and abort the run under pipefail.
missing_keys="$(LC_ALL=C comm -23 \
    <(LC_ALL=C sort "$VOLUME_WORK_DIR/data-protection-keys.pre-backup.manifest") \
    <(LC_ALL=C sort "$VOLUME_WORK_DIR/data-protection-keys.post-upgrade.manifest") | wc -l | tr -d ' ')"
assert_equal "0" "$missing_keys" "upgrade keeps every pre-upgrade data-protection key unchanged"
assert_data_protection_round_trip primary "post-upgrade"
capture_logs "target-upgraded"

BROWSER_RESULT="failed"
run_browser_scenario upgraded UpgradedDatabaseBrowserTests
BROWSER_RESULT="passed"

capture_logs "target-after-browser"
check_upgraded_checkout
check_upgraded_outbox
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
schema_dump restore-postgres > "$REPORT_DIR/schema-post-restore.sql"
schema_verdict="identical"
cmp --silent "$REPORT_DIR/schema-pre-upgrade.sql" "$REPORT_DIR/schema-post-restore.sql" || schema_verdict="different"
assert_equal "identical" "$schema_verdict" "restored schema matches the pre-upgrade schema byte for byte"
table_fingerprints restore-postgres > "$REPORT_DIR/rows-post-restore.txt"
rows_verdict="identical"
cmp --silent "$REPORT_DIR/rows-pre-upgrade.txt" "$REPORT_DIR/rows-post-restore.txt" || rows_verdict="different"
assert_equal "identical" "$rows_verdict" "restored rows match per-table fingerprints"
assert_equal "$PRE_UPGRADE_IDENTITY" "$(identity_fingerprint restore-postgres)" "restored Identity roles and password hashes"

# Persistent volumes: the empty restore key ring must reject the payload (negative control) and
# must not gain a key from that attempt; then both archives are restored into the new volumes.
VOLUME_RESTORE_RESULT="failed"
dp_negative="accepted"
if ! data_protection_unprotect restore > /dev/null 2> "$LOG_DIR/data-protection-negative-control.log"; then
    dp_negative="rejected"
fi
assert_equal "rejected" "$dp_negative" "empty restore key ring cannot unprotect the value (negative control)"
assert_volume_empty restore data-protection-keys

restore_volumes_started="$(date +%s%3N)"
restore_volume uploads
restore_volume data-protection-keys
RESTORE_VOLUMES_MILLISECONDS="$(( $(date +%s%3N) - restore_volumes_started ))"
assert_data_protection_round_trip restore "post-restore"
DATA_PROTECTION_RESULT="passed"

export REHEARSAL_API_IMAGE="$BASELINE_API_IMAGE"
export REHEARSAL_STOREFRONT_IMAGE="$BASELINE_STOREFRONT_IMAGE"
export REHEARSAL_WEB_IMAGE="$BASELINE_WEB_IMAGE"
export REHEARSAL_DB_HOST="restore-postgres"
export REHEARSAL_UPLOADS_VOLUME="restore-uploads-data"
export REHEARSAL_DATA_PROTECTION_VOLUME="restore-data-protection-keys"
export REHEARSAL_REQUIRE_CONFIRMED_EMAIL=false REHEARSAL_EMAIL_ENABLED=false
dc --profile restore up --detach --force-recreate api storefront web
CAPTURE_API_LOGS=true
verify_baseline_application "post-restore"
assert_rotation "$(session_cookie "$RUNTIME_DIR/baseline-survivor")" "$RUNTIME_DIR/restored-session" \
    "restored baseline accepts the original pre-backup refresh cookie"
verify_upload_fixtures_served "post-restore baseline"
UPLOADS_RESULT="passed"
VOLUME_RESTORE_RESULT="passed"
capture_logs "restored-baseline"

REHEARSAL_RESULT="passed"
echo "PASS: hosted staging upgrade, browser, backup restore, and restored baseline application rehearsal"
