# Publish local EOIR data to production

Run this after importing and verifying EOIR in the local `meridian` database.
The script replaces production **judges and court_cases only**, including all
rows currently in those tables. Schedule a maintenance window: queries using
these tables can wait while the replacement transaction runs.

## One-time setup

```sh
cd ~/source/repos/Meridian/src/Meridian.Importer
python3 -m venv .venv-eoir
.venv-eoir/bin/python -m pip install psycopg2-binary
```

## Upload

```sh
cd ~/source/repos/Meridian/src/Meridian.Importer
caffeinate -i .venv-eoir/bin/python -u scripts/push_eoir_to_production.py
```

Paste the production PostgreSQL URL at the hidden prompt. Alternatively, supply
it through `PROD_URL` using your secret manager. Credentials are not embedded in
the script or exported files. Include `sslmode=require` in the URL for encrypted
remote connections.

The default local connection is `dbname=meridian user=postgres host=localhost`.
Use `--local-dsn 'dbname=meridian user=postgres host=localhost'` to change it;
use `.pgpass` for a local password if required.

The script:

1. Checks column compatibility and table dependencies.
2. Exports a consistent snapshot of both local tables.
3. Backs up both production tables before changing them.
4. Locks both production tables and truncates them together, without CASCADE.
5. Loads judges first, then cases; bulk-rebuilds the existing secondary case
   indexes and validates the judge foreign key inside the same transaction.
6. Recalculates judge statistics, verifies case/judge counts and decided-case
   totals, then commits and runs ANALYZE.

The foreign key is temporarily removed and restored **inside the transaction**
to avoid accumulating millions of deferred COPY checks; other sessions cannot
observe an intermediate state. Errors before commit roll back the replacement.
If a connection fails during commit, check the database before retrying because
the commit outcome can be uncertain.

Output goes to `~/MeridianData/exports/eoir-YYYYMMDD-HHMMSS/`:

- `local-export/`: compressed CSV data plus a schema/count manifest.
- `production-backup/`: the previous production data and manifest.
- `SUCCESS.json`: counts saved after commit and ANALYZE finish.

These are **data-only backups for this script**, not pg_restore archives or full
database backups. Keep the production schema and save the backup directory.
The CSV transfer works across the local PostgreSQL 17 and production PostgreSQL
18 versions without requiring a matching pg_dump binary. Allow several GB of
local free space and sufficient production space for transactional replacement
and index rebuilding. Keep other EOIR imports and schema changes stopped during
the run; the advisory lock serializes this script's replacement phase only.

## Back up production without uploading

```sh
caffeinate -i .venv-eoir/bin/python -u scripts/push_eoir_to_production.py --backup-only
```

## Restore a previous production backup

Replace the timestamp with the actual saved directory. This also backs up the
current production data before restoring, and recalculates judge statistics.

```sh
caffeinate -i .venv-eoir/bin/python -u scripts/push_eoir_to_production.py \
  --restore-from ~/MeridianData/exports/eoir-YYYYMMDD-HHMMSS/production-backup
```

## Verify manually

Connect to production with your PostgreSQL client and run:

```sql
SELECT COUNT(*) AS court_cases,
       COUNT(*) FILTER (WHERE "Outcome" IS NOT NULL) AS cases_with_outcome
FROM public.court_cases;

SELECT COUNT(*) AS judges,
       SUM("TotalCasesDecided") AS decided_cases,
       MIN("StatsLastRefreshed") AS oldest_stats_refresh
FROM public.judges;
```

The two decided-case totals should match. The upload script also verifies both
table row counts against the local export before committing. There is no need to
run the judge statistics UPDATE separately.
