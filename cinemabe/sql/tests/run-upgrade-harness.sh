#!/usr/bin/env bash
# Builds a database from the OLD create_db.sql (git history), seeds rows the destructive guarded blocks of the
# CURRENT upgrade_db.sql must act on, runs upgrade_db.sql twice and asserts the result.
# Used by the CI job `db-upgrade` and locally.
#
# Environment:
#   SQL_SERVER    server for sqlcmd -S            (default localhost,1433)
#   SQL_USER      login                           (default sa)
#   SQL_PASSWORD  password                        (required)
#   HARNESS_DB    scratch database name           (default CinemaUpgradeHarness; dropped and re-created)
#   OLD_COMMIT    commit whose create_db.sql is the old schema
#                 (default 3656d73b2f37ee0806382f34f4149f0d128ccb94, master before the discount/gift-card/child-ticket work)
#   UPGRADE_SQL   upgrade script to test           (default: ../upgrade_db.sql relative to this script)
#   KEEP_DB=1     do not drop the scratch database at the end
set -euo pipefail

: "${SQL_PASSWORD:?SQL_PASSWORD is required}"
SQL_SERVER="${SQL_SERVER:-localhost,1433}"
SQL_USER="${SQL_USER:-sa}"
HARNESS_DB="${HARNESS_DB:-CinemaUpgradeHarness}"
OLD_COMMIT="${OLD_COMMIT:-3656d73b2f37ee0806382f34f4149f0d128ccb94}"

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sql_dir="$(cd "$here/.." && pwd)"
UPGRADE_SQL="${UPGRADE_SQL:-$sql_dir/upgrade_db.sql}"

# sqlcmd needs native paths on Windows (Git Bash); cygpath is absent on Linux.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }

# -I: QUOTED_IDENTIFIER ON (filtered indexes); -b: stop on error; -f 65001: UTF-8 input.
run() { sqlcmd -C -I -b -S "$SQL_SERVER" -U "$SQL_USER" -P "$SQL_PASSWORD" -f 65001 "$@"; }
runfile() { local db="$1" file="$2"; shift 2; run -d "$db" -i "$(native "$file")" "$@"; }

tmp="$(mktemp -d)"
cleanup() {
    rm -rf "$tmp"
    if [ "${KEEP_DB:-0}" != "1" ]; then
        run -d master -Q "IF DB_ID(N'$HARNESS_DB') IS NOT NULL BEGIN ALTER DATABASE [$HARNESS_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$HARNESS_DB]; END" >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

git -C "$here" cat-file -e "$OLD_COMMIT^{commit}" 2>/dev/null \
    || { echo "commit $OLD_COMMIT not found - fetch full history (actions/checkout fetch-depth: 0)" >&2; exit 2; }
git -C "$here" show "$OLD_COMMIT:cinemabe/sql/create_db.sql" > "$tmp/old_create_db.sql"

echo "== create scratch database $HARNESS_DB =="
run -d master -Q "IF DB_ID(N'$HARNESS_DB') IS NOT NULL BEGIN ALTER DATABASE [$HARNESS_DB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$HARNESS_DB]; END; CREATE DATABASE [$HARNESS_DB];"

echo "== build OLD schema ($OLD_COMMIT) =="
runfile "$HARNESS_DB" "$tmp/old_create_db.sql" >/dev/null

echo "== seed =="
runfile "$HARNESS_DB" "$here/seed-old-schema.sql"

echo "== upgrade run 1 =="
runfile "$HARNESS_DB" "$UPGRADE_SQL" >/dev/null
echo "== assert after run 1 =="
runfile "$HARNESS_DB" "$here/assert-after-upgrade.sql" -v Phase=first

echo "== upgrade run 2 =="
runfile "$HARNESS_DB" "$UPGRADE_SQL" >/dev/null
echo "== assert after run 2 =="
runfile "$HARNESS_DB" "$here/assert-after-upgrade.sql" -v Phase=second

echo "UPGRADE HARNESS PASSED"
