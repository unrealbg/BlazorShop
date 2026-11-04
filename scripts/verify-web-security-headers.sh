#!/usr/bin/env bash
# Verifies that the Web (WebAssembly workspace) nginx host sends its shared security
# headers on every static/cache/fallback route family, not only on plain files.
# Regression check for RC audit R04 (nginx add_header inheritance).
#
# Usage: scripts/verify-web-security-headers.sh http://127.0.0.1:18081
set -euo pipefail

base_url="${1:?usage: $0 <base-url>}"
base_url="${base_url%/}"
headers_file="$(mktemp)"
trap 'rm -f "$headers_file"' EXIT

# Wait for the container to accept requests.
curl --silent --output /dev/null --retry 15 --retry-all-errors --retry-connrefused --retry-delay 1 --max-time 5 "$base_url/index.html"

expected_headers=(
  'X-Content-Type-Options: nosniff'
  'X-Frame-Options: DENY'
  'Referrer-Policy: strict-origin-when-cross-origin'
  'Permissions-Policy: camera=(), geolocation=(), microphone=(), payment=(), usb=()'
)

# route|expected status|expected Cache-Control (empty = not asserted)
routes=(
  '/|200|no-cache'
  '/index.html|200|no-cache'
  '/account/profile|200|no-cache'
  '/css/site.css|200|no-cache'
  '/js/checkoutUi.js|200|no-cache'
  '/missing-r04-check.css|404|no-cache'
)

failures=0
for entry in "${routes[@]}"; do
  IFS='|' read -r route expected_status expected_cache <<< "$entry"
  status="$(curl --silent --show-error --max-time 5 --output /dev/null --dump-header "$headers_file" --write-out '%{http_code}' "$base_url$route")"
  if [[ "$status" != "$expected_status" ]]; then
    echo "FAIL $route: expected HTTP $expected_status, got $status"
    failures=$((failures + 1))
    continue
  fi

  for header in "${expected_headers[@]}"; do
    if ! tr -d '\r' < "$headers_file" | grep -qixF "$header"; then
      echo "FAIL $route: missing '$header'"
      failures=$((failures + 1))
    fi
  done

  if [[ -n "$expected_cache" ]] && ! tr -d '\r' < "$headers_file" | grep -qix "Cache-Control: $expected_cache"; then
    echo "FAIL $route: missing 'Cache-Control: $expected_cache'"
    failures=$((failures + 1))
  fi

  echo "ok   $route (HTTP $status)"
done

if (( failures > 0 )); then
  echo "$failures security header check(s) failed."
  exit 1
fi

echo "All Web security header checks passed."
