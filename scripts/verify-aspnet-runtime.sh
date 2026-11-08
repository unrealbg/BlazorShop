#!/usr/bin/env bash
set -Eeuo pipefail

image="${1:?Usage: verify-aspnet-runtime.sh <image>}"
runtimes="$(docker run --rm --pull=never --network=none --entrypoint dotnet "$image" --list-runtimes)"
printf '%s\n' "$runtimes"

# Assert the runtime inside the final image, including builds that override a base-image ARG.
expected_version=10.0.14
for framework in Microsoft.AspNetCore.App Microsoft.NETCore.App; do
    actual="$(awk -v framework="$framework" '$1 == framework { print $2 }' <<< "$runtimes")"
    if [[ "$actual" != "$expected_version" ]]; then
        printf 'Expected %s %s in %s, found %s\n' "$framework" "$expected_version" "$image" "$actual" >&2
        exit 1
    fi
done
