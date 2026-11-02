#!/usr/bin/env bash
set -euo pipefail

/usr/local/firebird/docker-entrypoint.sh firebird &
firebird_pid=$!

cleanup() {
    kill "$firebird_pid" 2>/dev/null || true
    wait "$firebird_pid" || true
}

trap cleanup INT TERM

database="/firebird/data/${FIREBIRD_DATABASE:-northstar.fdb}"
for _ in $(seq 1 60); do
    if printf 'show tables;\n' | /usr/local/firebird/bin/isql \
        -user "${ISC_USER:-SYSDBA}" \
        -password "${ISC_PASSWORD}" \
        "$database" -q >/dev/null 2>&1; then
        break
    fi
    sleep 1
done

if ! printf 'show tables;\n' | /usr/local/firebird/bin/isql \
    -user "${ISC_USER:-SYSDBA}" \
    -password "${ISC_PASSWORD}" \
    "$database" -q | grep -q '^EMPLOYEES'; then
    /usr/local/firebird/bin/isql \
        -user "${ISC_USER:-SYSDBA}" \
        -password "${ISC_PASSWORD}" \
        "$database" -q -i /scripts/northstar-demo.firebird.sql
fi

wait "$firebird_pid"
