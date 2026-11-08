# Sourced by the isolated GitHub-hosted rehearsal runner.

qualification_init() {
    mkdir -p "$REHEARSAL_EDGE_DIR"
    openssl req -x509 -newkey rsa:2048 -noenc -days 1 -subj '/CN=localhost' \
        -addext 'subjectAltName=DNS:localhost' -keyout "$REHEARSAL_EDGE_DIR/tls.key" \
        -out "$REHEARSAL_EDGE_DIR/tls.crt" 2> "$LOG_DIR/edge-certificate.log"
    cat > "$REHEARSAL_EDGE_DIR/default.conf" <<'NGINX'
server {
    listen 443 ssl;
    server_name localhost;
    ssl_certificate /edge/tls.crt;
    ssl_certificate_key /edge/tls.key;
    resolver 127.0.0.11 valid=2s ipv6=off;
    proxy_set_header Host $http_host;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_set_header X-Forwarded-For $remote_addr;
    if (-f /edge/maintenance) { return 503; }
    location ~ ^(/health|/alive|/api/) {
        set $backend http://api:8080;
        proxy_pass $backend;
    }
    location /authentication/ {
        set $backend http://web:80;
        proxy_pass $backend;
    }
    location / {
        set $backend http://storefront:8080;
        proxy_pass $backend;
    }
}
NGINX
}

edge_request() {
    curl --silent --show-error --max-time 12 --cacert "$REHEARSAL_EDGE_DIR/tls.crt" "$@"
}

qualification_maintenance() {
    local edge="https://localhost:$REHEARSAL_EDGE_PORT"
    dc --profile edge up --detach edge
    local status
    for _ in {1..15}; do
        status="$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/health" 2>/dev/null || :)"
        [[ "$status" == 200 ]] && break
        sleep 1
    done
    assert_equal 200 "$status" "trusted test TLS edge routes to baseline API"
    assert_equal 200 "$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/product/rehearsal-canvas-bag")" "TLS edge routes to baseline Storefront"
    assert_equal 200 "$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/authentication/login/account")" "TLS edge routes to baseline Web"
    touch "$REHEARSAL_EDGE_DIR/maintenance"
    assert_equal 503 "$(edge_request --output /dev/null --write-out '%{http_code}' \
        --header 'Content-Type: application/json' --data '{}' "$edge/api/authentication/create")" \
        "maintenance edge rejects a new write request"
}

qualification_aggregate() {
    local service="$1" phase="$2" count="$3"
    local report="$REPORT_DIR/preflight-$phase.txt"
    psql_service "$service" --quiet --no-align --tuples-only --field-separator '|' \
        < "$TOOLING_DIR/rehearsal/recovery-preflight.sql" > "$report"
    assert_equal 'transaction_read_only|on' "$(head -n 1 "$report")" "$phase aggregate SQL is read only"
    assert_equal "migration.count|$count" "$(grep '^migration.count|' "$report")" "$phase aggregate SQL sees the expected migration count"
}

qualification_drain() {
    local running
    running="$(dc ps --services --status running)"
    assert_equal "" "$(grep -E '^(api|storefront|web)$' <<< "$running" || :)" "application processes and hosted workers stopped"
    assert_equal 0 "$(scalar postgres "SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND backend_type='client backend';")" \
        "no other database client remains after synthetic writer stop"
}

qualification_checkpoint() {
    local started="$(date +%s%3N)"
    (
        cd "$RUNTIME_DIR"
        cp "$COMPOSE_FILE" checkpoint-compose.yml
        docker image save --output rollback-images.tar "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE"
        sha256sum baseline.dump volumes/*.tar.gz checkpoint-compose.yml rollback-images.tar > checkpoint.sha256
        sha256sum --check checkpoint.sha256 > /dev/null
    )
    for image in "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE"; do
        docker image inspect "$image" --format '{{.Id}}'
    done > "$RUNTIME_DIR/baseline-ids.txt"
    {
        printf 'checkpoint_config_and_image_export_ms=%s\n' "$(( $(date +%s%3N) - started ))"
        printf 'docker_archive_bytes=%s\n' "$(stat -c %s "$RUNTIME_DIR/rollback-images.tar")"
        printf 'database_archive_bytes=%s\n' "$(stat -c %s "$BACKUP_PATH")"
        cat "$RUNTIME_DIR/baseline-ids.txt"
        cat "$RUNTIME_DIR/checkpoint.sha256"
    } > "$REPORT_DIR/recovery-qualification.txt"
}

qualification_before_migration() {
    dc up --detach --pull never api
    wait_http "$REHEARSAL_API_URL/health" "baseline can restart before migration"
    verify_baseline_database postgres
    stop_application_writers
}

qualification_failed_startup() {
    local cid code
    export REHEARSAL_DB_LOGIN_USER=rehearsal_missing_login
    dc up --detach --no-deps --force-recreate --pull never api
    cid="$(dc ps --all --quiet api)"
    code="$(timeout 90 docker wait "$cid")"
    [[ "$code" =~ ^[1-9][0-9]*$ ]]
    docker logs "$cid" > "$LOG_DIR/synthetic-startup-failure.log" 2>&1
    grep -Eq '28P01|password authentication failed' "$LOG_DIR/synthetic-startup-failure.log"
    assert_equal 5 "$(scalar postgres 'SELECT count(*) FROM "__EFMigrationsHistory";')" "failed startup leaves five baseline migrations"
    assert_equal 503 "$(edge_request --output /dev/null --write-out '%{http_code}' \
        "https://localhost:$REHEARSAL_EDGE_PORT/health")" "maintenance stays closed during failed startup"
    dc rm --force api
    unset REHEARSAL_DB_LOGIN_USER
}

qualification_forwarding() {
    local cid status edge="https://localhost:$REHEARSAL_EDGE_PORT"
    cid="$(dc --profile edge ps --quiet edge)"
    REHEARSAL_EDGE_IP="$(docker inspect "$cid" --format '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}')"
    [[ "$REHEARSAL_EDGE_IP" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]]
    export REHEARSAL_EDGE_IP REHEARSAL_FORCE_HTTPS=true REHEARSAL_FORWARD_HEADERS=true
    dc up --detach --force-recreate --no-deps --pull never api
    rm "$REHEARSAL_EDGE_DIR/maintenance"
    for _ in {1..35}; do
        status="$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/health" 2>/dev/null || :)"
        [[ "$status" == 200 ]] && break
        sleep 1
    done
    assert_equal 200 "$status" "HTTPS readiness succeeds through the trusted forwarding hop"
    assert_equal 307 "$(curl -sS --max-time 10 --header 'X-Forwarded-Proto: https' \
        --output /dev/null --write-out '%{http_code}' "$REHEARSAL_API_URL/health")" "untrusted direct request cannot spoof HTTPS forwarding"
    assert_equal 200 "$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/alive")" "target liveness succeeds through the TLS edge"
    assert_equal 200 "$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/product/rehearsal-canvas-bag")" "target Storefront is reachable through TLS"
    assert_equal 200 "$(edge_request --output /dev/null --write-out '%{http_code}' "$edge/authentication/login/account")" "target Web is reachable through TLS"
    touch "$REHEARSAL_EDGE_DIR/maintenance"
    unset REHEARSAL_EDGE_IP REHEARSAL_FORCE_HTTPS REHEARSAL_FORWARD_HEADERS
}

qualification_restore_images() {
    local started="$(date +%s%3N)" image
    (cd "$RUNTIME_DIR"; sha256sum --check checkpoint.sha256 > /dev/null)
    docker image rm "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE" > "$LOG_DIR/recovery-untag.log"
    docker image load --input "$RUNTIME_DIR/rollback-images.tar" > "$LOG_DIR/recovery-load.log"
    for image in "$BASELINE_API_IMAGE" "$BASELINE_STOREFRONT_IMAGE" "$BASELINE_WEB_IMAGE"; do
        docker image inspect "$image" --format '{{.Id}}'
    done > "$RUNTIME_DIR/restored-ids.txt"
    cmp "$RUNTIME_DIR/baseline-ids.txt" "$RUNTIME_DIR/restored-ids.txt"
    assert_equal 3 "$(wc -l < "$RUNTIME_DIR/restored-ids.txt" | tr -d ' ')" "all archived baseline image IDs reload without rebuild"
    cp "$RUNTIME_DIR/checkpoint-compose.yml" "$RUNTIME_DIR/restored-compose.yml"
    cmp "$COMPOSE_FILE" "$RUNTIME_DIR/restored-compose.yml"
    RECOVERED_COMPOSE_FILE="$RUNTIME_DIR/restored-compose.yml"
    printf 'checkpoint_hash_and_image_reload_ms=%s\n' "$(( $(date +%s%3N) - started ))" >> "$REPORT_DIR/recovery-qualification.txt"
}

send_synthetic_stripe_event() {
    local payload="$1" signature="$2"
    curl --silent --show-error --max-time 15 --output /dev/null --write-out '%{http_code}' \
        --header 'Content-Type: application/json' --header "Stripe-Signature: $signature" \
        --data-binary "$payload" "$REHEARSAL_API_URL/api/stripe/webhook"
}

check_upgraded_stripe_webhook() {
    local order_id="37000000-0000-0000-0000-000000000100"
    local transaction_id="37000000-0000-0000-0000-000000000130"
    local reservation_id="37000000-0000-0000-0000-000000000131"
    local timestamp payload signature stock_before stock_reserved state_sql

    # Extend the isolated upgraded fixture after the original assertions. Outbound Stripe stays disabled.
    stock_before="$(scalar postgres "SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$SIMPLE_PRODUCT_ID';")"
    stock_reserved="$((stock_before - 2))"
    psql_service postgres --quiet \
        --set "order_id=$order_id" --set "transaction_id=$transaction_id" \
        --set "reservation_id=$reservation_id" --set "product_id=$SIMPLE_PRODUCT_ID" <<'SQL'
BEGIN;
UPDATE "Orders" SET "OrderStatus" = 'Pending', "PaymentMethod" = 'Stripe', "FulfillmentStatus" = 'NotStarted'
WHERE "Id" = :'order_id';
-- Reserving stock decrements availability. Consuming the existing reservation does not decrement it again.
UPDATE "Products" SET "Quantity" = "Quantity" - 2 WHERE "Id" = :'product_id';
INSERT INTO "PaymentTransactions" ("Id", "OrderId", "Provider", "ProviderSessionId",
    "ProviderPaymentIntentId", "ExpectedAmountMinor", "Currency", "Status")
VALUES (:'transaction_id', :'order_id', 'Stripe', 'cs_rehearsal', 'pi_rehearsal', 2500, 'EUR', 'Pending');
INSERT INTO "InventoryReservations" ("Id", "OrderId", "ProductId", "Quantity", "Status")
VALUES (:'reservation_id', :'order_id', :'product_id', 2, 'Reserved');
COMMIT;
SQL
    state_sql="SELECT o.\"PaymentStatus\", p.\"Status\", r.\"Status\",
       (SELECT count(*) FROM \"PaymentProviderEvents\" WHERE \"ProviderEventId\" = 'evt_r09_rehearsal'),
       (SELECT \"Quantity\" FROM \"Products\" WHERE \"Id\" = '$SIMPLE_PRODUCT_ID')
       FROM \"Orders\" o JOIN \"PaymentTransactions\" p ON p.\"OrderId\" = o.\"Id\"
       JOIN \"InventoryReservations\" r ON r.\"OrderId\" = o.\"Id\"
       WHERE o.\"Id\" = '$order_id' AND p.\"Id\" = '$transaction_id' AND r.\"Id\" = '$reservation_id';"
    assert_equal "Pending|Pending|Reserved|0|$stock_reserved" "$(scalar postgres "$state_sql")" "synthetic Stripe fixture starts pending with stock reserved"

    timestamp="$(date +%s)"
    # Stripe.net 50.0.0 expects the 2025-11-17.clover API version.
    payload="$(jq -cn --argjson time "$timestamp" --arg order "$order_id" --arg transaction "$transaction_id" '{
        id: "evt_r09_rehearsal", object: "event", api_version: "2025-11-17.clover", request: null,
        created: $time, type: "checkout.session.completed", data: { object: {
            id: "cs_rehearsal", object: "checkout.session", amount_total: 2500, currency: "eur",
            client_reference_id: $order, payment_intent: "pi_rehearsal", payment_status: "paid", status: "complete",
            metadata: { order_id: $order, payment_transaction_id: $transaction }
        }}
    }')"
    signature="$(printf '%s.%s' "$timestamp" "$payload" \
        | openssl dgst -sha256 -hmac "$REHEARSAL_STRIPE_WEBHOOK_SECRET" | awk '{print $NF}')"
    assert_equal 400 "$(send_synthetic_stripe_event "$payload" "t=$timestamp,v1=invalid")" "invalid fake Stripe signature is rejected"
    assert_equal "Pending|Pending|Reserved|0|$stock_reserved" "$(scalar postgres "$state_sql")" "invalid signature leaves ledger, payment and stock unchanged"
    assert_equal 200 "$(send_synthetic_stripe_event "$payload" "t=$timestamp,v1=$signature")" "signed fake Stripe event accepted by verified OCI API"
    assert_equal "Paid|Paid|Consumed|1|$stock_reserved" "$(scalar postgres "$state_sql")" "Stripe reconciliation consumes the reservation without a second stock decrement"
    assert_equal 200 "$(send_synthetic_stripe_event "$payload" "t=$timestamp,v1=$signature")" "fake Stripe retry is acknowledged"
    assert_equal "Paid|Paid|Consumed|1|$stock_reserved" "$(scalar postgres "$state_sql")" "fake Stripe retry leaves stock and ledger unchanged"
}

qualification_reconcile() {
    local mail_count
    mail_count="$(curl -fsS --max-time 10 "http://localhost:$REHEARSAL_MAIL_PORT/api/v1/messages" \
        | jq -er '.messages | length')"
    assert_equal 1 "$mail_count" "external synthetic confirmation persists after database restore"
    assert_equal t "$(scalar restore-postgres 'SELECT to_regclass('\''public."PaymentProviderEvents"'\'') IS NULL;')" \
        "restored baseline has no target Stripe provider ledger"
    assert_equal 2 "$(scalar restore-postgres 'SELECT count(*) FROM "AspNetUsers";')" \
        "restore excludes the post-checkpoint registration"
    printf 'signed_provider_paid_event_requires_reconciliation=1\nexternal_mail_after_restore=%s\n' "$mail_count" \
        >> "$REPORT_DIR/recovery-qualification.txt"
}
