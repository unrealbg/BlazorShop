#!/usr/bin/env bash
# Sourced by run-staging-rehearsal.sh after it establishes the isolated project.

login_user() {
    local email="$1" password="$2" session="$3" url="${4:-$REHEARSAL_API_URL}"
    local status
    status="$(jq -cn --arg email "$email" --arg password "$password" '{email:$email,password:$password}' |
        curl -sS --max-time 20 -o "$session.json" -D "$session.headers" -w '%{http_code}' \
            -H 'Content-Type: application/json' --data-binary @- "$url/api/authentication/login")"
    assert_equal 200 "$status" "synthetic login ($(basename "$session"))"
    assert_equal true "$(jq -r '.success and (.token | type == "string" and length > 0)' "$session.json")" \
        "login returns an access token ($(basename "$session"))"
}

session_cookie() {
    sed -nE 's/^Set-Cookie: (blazorshop-rehearsal-refresh=[^;]+);.*/\1/ip' "$1.headers"
}

auth_post() {
    local action="$1" cookie="$2" session="$3"
    curl -sS --max-time 20 -o "$session.json" -D "$session.headers" -w '%{http_code}' \
        -X POST -H "Cookie: $cookie" "$REHEARSAL_API_URL/api/authentication/$action"
}

assert_rotation() {
    local original="$1" replacement="$2" description="$3"
    assert_equal 200 "$(auth_post refresh-token "$original" "$replacement")" "$description"
    local cookie
    cookie="$(session_cookie "$replacement")"
    assert_equal yes "$([[ -n "$cookie" && "$cookie" != "$original" ]] && echo yes || echo no)" \
        "$description issues a different cookie"
}

identity_fingerprint() {
    local service="$1"
    scalar "$service" "
        SELECT u.\"Id\", u.\"Email\", u.\"EmailConfirmed\", u.\"PasswordHash\", r.\"Name\"
        FROM \"AspNetUsers\" u
        JOIN \"AspNetUserRoles\" ur ON ur.\"UserId\" = u.\"Id\"
        JOIN \"AspNetRoles\" r ON r.\"Id\" = ur.\"RoleId\"
        WHERE u.\"Email\" IN ('$USER_EMAIL', '$ADMIN_EMAIL') ORDER BY u.\"Id\", r.\"Name\";" |
        sha256sum | cut -d ' ' -f 1
}

verify_role() {
    local email="$1" expected="$2"
    assert_equal "$expected" "$(scalar postgres "
        SELECT r.\"Name\" FROM \"AspNetRoles\" r
        JOIN \"AspNetUserRoles\" ur ON ur.\"RoleId\" = r.\"Id\"
        JOIN \"AspNetUsers\" u ON u.\"Id\" = ur.\"UserId\"
        WHERE u.\"Email\" = '$email' ORDER BY r.\"Name\";")" "first/second registration role ($expected)"
}

check_pre_upgrade_sessions() {
    local session="$RUNTIME_DIR/baseline-session"
    login_user "$USER_EMAIL" "$USER_PASSWORD" "$session"
    local cookie
    cookie="$(session_cookie "$session")"
    assert_equal yes "$([[ -n "$cookie" ]] && echo yes || echo no)" "baseline login sets a refresh cookie"
    assert_rotation "$cookie" "$RUNTIME_DIR/baseline-rotation" "baseline refresh rotation"
    assert_equal 400 "$(auth_post refresh-token 'blazorshop-rehearsal-refresh=invalid' "$RUNTIME_DIR/invalid")" \
        "baseline rejects a tampered refresh cookie"
    login_user "$USER_EMAIL" "$USER_PASSWORD" "$RUNTIME_DIR/baseline-survivor"
    assert_equal yes "$([[ -n "$(session_cookie "$RUNTIME_DIR/baseline-survivor")" ]] && echo yes || echo no)" \
        "baseline session retained for upgrade and restore"
}

check_upgraded_sessions() {
    local original="$1" replacement="$RUNTIME_DIR/upgraded-rotation"
    assert_rotation "$original" "$replacement" "target rotates a pre-upgrade refresh cookie"
    assert_equal 400 "$(auth_post refresh-token "$original" "$RUNTIME_DIR/replay-parent")" \
        "target rejects replay of the pre-upgrade cookie"
    assert_equal 400 "$(auth_post refresh-token "$(session_cookie "$replacement")" "$RUNTIME_DIR/replay-child")" \
        "target replay revokes the successor"

    local session="$RUNTIME_DIR/concurrent-session"
    login_user "$USER_EMAIL" "$USER_PASSWORD" "$session"
    local cookie first second
    cookie="$(session_cookie "$session")"
    auth_post refresh-token "$cookie" "$RUNTIME_DIR/race-a" > "$RUNTIME_DIR/race-a.status" &
    first=$!
    auth_post refresh-token "$cookie" "$RUNTIME_DIR/race-b" > "$RUNTIME_DIR/race-b.status" &
    second=$!
    wait "$first"
    wait "$second"
    assert_equal "200 400" "$(printf '%s\n' "$(cat "$RUNTIME_DIR/race-a.status")" "$(cat "$RUNTIME_DIR/race-b.status")" | sort | paste -sd ' ')" \
        "concurrent target refresh grants exactly one successor"

    login_user "$USER_EMAIL" "$USER_PASSWORD" "$RUNTIME_DIR/logout-session"
    cookie="$(session_cookie "$RUNTIME_DIR/logout-session")"
    assert_equal 200 "$(auth_post logout "$cookie" "$RUNTIME_DIR/logout-result")" "target logout accepts current cookie"
    assert_equal 400 "$(auth_post refresh-token "$cookie" "$RUNTIME_DIR/logged-out")" "target logout revokes refresh"
}

checkout_request() {
    local token="$1" key="$2" quantity="$3" output="$4"
    jq -cn --arg product "$VARIANT_PRODUCT_ID" --arg variant "$VARIANT_ID" --argjson quantity "$quantity" \
        '{paymentMethodId:"6f2c2a7e-9f9b-4a0d-9f7f-2a1b3c4d5e6f",carts:[{productId:$product,variantId:$variant,quantity:$quantity}]}' |
        curl -sS --max-time 30 -o "$output.json" -D "$output.headers" -w '%{http_code}' \
            -H "Authorization: Bearer $token" -H "Idempotency-Key: $key" \
            -H 'Content-Type: application/json' --data-binary @- "$REHEARSAL_WEB_URL/api/cart/checkout"
}

check_upgraded_checkout() {
    local token key order first second
    login_user "$USER_EMAIL" "$USER_PASSWORD" "$RUNTIME_DIR/checkout-session" "$REHEARSAL_WEB_URL"
    token="$(jq -er '.token' "$RUNTIME_DIR/checkout-session.json")"
    key="$(cat /proc/sys/kernel/random/uuid)"
    assert_equal 200 "$(checkout_request "$token" "$key" 6 "$RUNTIME_DIR/order")" "upgraded checkout confirms offline COD"
    order="$(jq -er '.payload.orderId' "$RUNTIME_DIR/order.json")"
    [[ "$order" =~ ^[0-9a-f-]{36}$ ]]
    assert_equal 200 "$(checkout_request "$token" "$key" 6 "$RUNTIME_DIR/order-replay")" "checkout idempotency replay succeeds"
    assert_equal "$order" "$(jq -er '.payload.orderId' "$RUNTIME_DIR/order-replay.json")" "checkout replay returns the same order"
    assert_equal yes "$(grep -Eqi '^Idempotency-Replayed: true' "$RUNTIME_DIR/order-replay.headers" && echo yes || echo no)" "checkout replay is explicitly identified"
    assert_equal 409 "$(checkout_request "$token" "$key" 5 "$RUNTIME_DIR/order-conflict")" "same key with a different intent conflicts"
    assert_equal "$VARIANT_ID|REHEARSAL-US10-BLUE|31.25|6" "$(scalar postgres "
        SELECT \"ProductVariantId\", \"SkuSnapshot\", to_char(\"UnitPrice\", 'FM999999990.00'), \"Quantity\"
        FROM \"OrderLines\" WHERE \"OrderId\" = '$order';")" "new order persists purchase snapshot and variant identity"
    assert_equal 1 "$(scalar postgres "SELECT \"Stock\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID';")" \
        "idempotent checkout consumes stock only once"

    checkout_request "$token" "$(cat /proc/sys/kernel/random/uuid)" 1 "$RUNTIME_DIR/stock-a" > "$RUNTIME_DIR/stock-a.status" &
    first=$!
    checkout_request "$token" "$(cat /proc/sys/kernel/random/uuid)" 1 "$RUNTIME_DIR/stock-b" > "$RUNTIME_DIR/stock-b.status" &
    second=$!
    wait "$first"
    wait "$second"
    assert_equal "200 400" "$(printf '%s\n' "$(cat "$RUNTIME_DIR/stock-a.status")" "$(cat "$RUNTIME_DIR/stock-b.status")" | sort | paste -sd ' ')" \
        "one checkout wins the last unit, the other is rejected"
    assert_equal "0|2|2" "$(scalar postgres "
        SELECT (SELECT \"Stock\" FROM \"ProductVariants\" WHERE \"Id\" = '$VARIANT_ID'),
               (SELECT COUNT(*) FROM \"OrderLines\" WHERE \"ProductVariantId\" = '$VARIANT_ID'),
               (SELECT COUNT(*) FROM \"InventoryReservations\" WHERE \"ProductVariantId\" = '$VARIANT_ID');")" \
        "overselling check leaves exactly two new orders and reservations and zero stock"
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

check_upgraded_outbox() {
    local email="rehearsal.outbox.${RUN_NUMBER}.${RUN_ATTEMPT}@example.test"
    local password user_id mail_id state token status
    password="$(generate_user_password)"
    local mail_url="http://localhost:$REHEARSAL_MAIL_PORT"
    dc --profile mail up --detach mailpit
    wait_http "$mail_url/api/v1/messages" "local SMTP sink"
    export REHEARSAL_REQUIRE_CONFIRMED_EMAIL=true REHEARSAL_EMAIL_ENABLED=true
    # This phase processes confirmation URLs. Keep its HTTP logs and mail bodies out of artifacts.
    CAPTURE_API_LOGS=false
    dc up --detach --force-recreate api
    wait_http "$REHEARSAL_API_URL/health" "target API with local SMTP only"
    register_user "$email" "$password"
    user_id="$(scalar postgres "SELECT \"Id\" FROM \"AspNetUsers\" WHERE \"Email\" = '$email';")"
    [[ "$user_id" =~ ^[0-9a-f-]{36}$ ]]
    state=""
    for _ in {1..45}; do
        state="$(scalar postgres "SELECT \"State\", \"Generation\", \"AttemptCount\" FROM \"RegistrationConfirmationDeliveries\" WHERE \"UserId\" = '$user_id';")"
        [[ "$state" == "2|1|1" ]] && break
        sleep 1
    done
    assert_equal "2|1|1" "$state" "target outbox delivers one first-generation attempt"
    curl -fsS --max-time 10 "$mail_url/api/v1/messages" -o "$RUNTIME_DIR/mailbox.json"
    assert_equal 1 "$(jq -r --arg email "$email" '[.messages[] | select(any(.To[]; .Address == $email))] | length' "$RUNTIME_DIR/mailbox.json")" \
        "local SMTP sink received exactly one confirmation"
    mail_id="$(jq -er --arg email "$email" '.messages[] | select(any(.To[]; .Address == $email)) | .ID' "$RUNTIME_DIR/mailbox.json")"
    [[ "$mail_id" =~ ^[0-9a-zA-Z-]+$ ]]
    curl -fsS --max-time 10 "$mail_url/api/v1/message/$mail_id" -o "$RUNTIME_DIR/mail.json"
    token="$(jq -er --arg user "$user_id" '
        .HTML | capture("/confirm-email\\?userId=(?<user>[^&]+)&amp;token=(?<token>[0-9]+)") |
        select(.user == $user) | .token' "$RUNTIME_DIR/mail.json")"
    status="$(curl -sS --max-time 15 -o /dev/null -w '%{http_code}' --get \
        --data-urlencode "userId=$user_id" --data-urlencode 'token=invalid' "$REHEARSAL_API_URL/api/authentication/confirm-email")"
    assert_equal 400 "$status" "real Identity confirmation endpoint rejects an invalid token"
    status="$(curl -sS --max-time 15 -o /dev/null -w '%{http_code}' --get \
        --data-urlencode "userId=$user_id" --data-urlencode "token=$token" "$REHEARSAL_API_URL/api/authentication/confirm-email")"
    assert_equal 200 "$status" "real Identity confirmation endpoint accepts the delivered token"
    assert_equal t "$(scalar postgres "SELECT \"EmailConfirmed\" FROM \"AspNetUsers\" WHERE \"Id\" = '$user_id';")" \
        "delivery activates the synthetic Identity account"
    login_user "$email" "$password" "$RUNTIME_DIR/confirmed-user"
}
