#!/usr/bin/env python3
"""Back up and atomically replace production EOIR tables from local Meridian.
Requires Python 3 and psycopg2-binary. Credentials come from PROD_URL or a hidden prompt.
Run --help for usage. No production changes occur until both exports finish.
"""
import argparse
import gzip
import getpass
import io
import json
import os
from pathlib import Path
import time

import psycopg2
from psycopg2 import sql

TABLES = ('judges', 'court_cases')


def log(message):
    print(time.strftime('%Y-%m-%d %H:%M:%S'), message, flush=True)


def connect(dsn):
    return psycopg2.connect(dsn, connect_timeout=30,
                           application_name='meridian-eoir-publish',
                           keepalives=1, keepalives_idle=30,
                           keepalives_interval=10, keepalives_count=6)


def table(name):
    return sql.Identifier('public', name)


def schema(cur):
    cur.execute("""SELECT table_name, column_name, data_type, udt_name,
        is_nullable, character_maximum_length, numeric_precision, numeric_scale
        FROM information_schema.columns WHERE table_schema='public'
        AND table_name IN ('judges','court_cases')
        ORDER BY table_name, ordinal_position""")
    return [list(row) for row in cur.fetchall()]


def counts(cur):
    result = {}
    for name in TABLES:
        cur.execute(sql.SQL('SELECT count(*) FROM {}').format(table(name)))
        result[name] = cur.fetchone()[0]
    cur.execute('SELECT count(*) FROM public.court_cases WHERE "Outcome" IS NOT NULL')
    result['decided_cases'] = cur.fetchone()[0]
    return result


def dependency_check(cur):
    cur.execute("""SELECT conrelid::regclass::text, confrelid::regclass::text,
        conname, pg_get_constraintdef(oid)
        FROM pg_constraint WHERE contype='f' AND
        (conrelid IN ('public.judges'::regclass,'public.court_cases'::regclass)
        OR confrelid IN ('public.judges'::regclass,'public.court_cases'::regclass))""")
    refs = cur.fetchall()
    if len(refs) != 1 or refs[0][:2] != ('court_cases', 'judges'):
        raise RuntimeError(f'Unexpected table dependencies; review before upload: {refs}')
    cur.execute("""SELECT tgname FROM pg_trigger WHERE NOT tgisinternal
        AND tgrelid IN ('public.judges'::regclass,'public.court_cases'::regclass)""")
    if cur.fetchall():
        raise RuntimeError('Custom triggers require review before upload')
    return refs[0]


def export(conn, directory, label):
    directory.mkdir(parents=True, exist_ok=False)
    conn.set_session(isolation_level='REPEATABLE READ', readonly=True)
    with conn.cursor() as cur:
        cur.execute("SET statement_timeout = 0")
        columns = schema(cur)
        metadata = {'schema': columns, 'counts': counts(cur), 'label': label}
        log(f'{label} counts: {metadata["counts"]}')
        for name in TABLES:
            names = [r[1] for r in columns if r[0] == name]
            command = sql.SQL('COPY {} ({}) TO STDOUT WITH (FORMAT CSV)').format(
                table(name), sql.SQL(',').join(map(sql.Identifier, names)))
            target = directory / f'{name}.csv.gz'
            log(f'Exporting {label} {name} to {target}')
            with gzip.open(target, 'wb', compresslevel=1) as compressed:
                with io.BufferedWriter(compressed, buffer_size=1024 * 1024) as output:
                    cur.copy_expert(command.as_string(conn), output)
            log(f'Exported {label} {name}: {target.stat().st_size:,} bytes')
        (directory / 'manifest.json').write_text(json.dumps(metadata, indent=2) + '\n')
    conn.commit()
    conn.set_session(isolation_level='READ COMMITTED', readonly=False)
    return metadata


def refresh_stats(cur):
    cur.execute('''UPDATE public.judges SET "TotalCasesDecided"=0,
        "GrantRatePercent"=0,"DenialRatePercent"=0,"RemovalOrderRatePercent"=0,
        "StatsLastRefreshed"=NOW()''')
    cur.execute('''UPDATE public.judges j SET "TotalCasesDecided"=s.total,
        "GrantRatePercent"=ROUND(s.grants*100.0/NULLIF(s.total,0),2),
        "DenialRatePercent"=ROUND(s.denials*100.0/NULLIF(s.total,0),2),
        "RemovalOrderRatePercent"=ROUND(s.removals*100.0/NULLIF(s.total,0),2)
        FROM (SELECT "JudgeId",COUNT(*) total,
        COUNT(*) FILTER (WHERE "Outcome"=0) grants,
        COUNT(*) FILTER (WHERE "Outcome"=1) denials,
        COUNT(*) FILTER (WHERE "Outcome"=2) removals
        FROM public.court_cases WHERE "Outcome" IS NOT NULL GROUP BY "JudgeId") s
        WHERE j."Id"=s."JudgeId"''')


def restore(conn, source, metadata):
    try:
        with conn.cursor() as cur:
            cur.execute('SET statement_timeout=0')
            cur.execute("SET lock_timeout='15s'")
            cur.execute('SELECT pg_advisory_xact_lock(742019)')
            log('Acquiring production table locks; queries may wait until commit')
            cur.execute('LOCK TABLE public.judges, public.court_cases IN ACCESS EXCLUSIVE MODE')
            if schema(cur) != metadata['schema']:
                raise RuntimeError('Production column schema differs from the export')
            ref = dependency_check(cur)
            # Preserve every index definition; rebuild secondary indexes in bulk.
            # Primary/constraint-backed indexes stay installed throughout.
            cur.execute("""SELECT ns.nspname, ix.relname, pg_get_indexdef(ix.oid)
                FROM pg_index i JOIN pg_class ix ON ix.oid=i.indexrelid
                JOIN pg_namespace ns ON ns.oid=ix.relnamespace
                WHERE i.indrelid='public.court_cases'::regclass
                AND NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conindid=i.indexrelid)
                ORDER BY ix.relname""")
            indexes = cur.fetchall()
            cur.execute(sql.SQL('ALTER TABLE public.court_cases DROP CONSTRAINT {}').format(sql.Identifier(ref[2])))
            for ns, name, definition in indexes:
                cur.execute(sql.SQL('DROP INDEX {}').format(sql.Identifier(ns, name)))
            # No CASCADE: unexpected references must fail rather than erase data.
            cur.execute('TRUNCATE TABLE public.court_cases, public.judges')
            for name in TABLES:
                names = [r[1] for r in metadata['schema'] if r[0] == name]
                command = sql.SQL('COPY {} ({}) FROM STDIN WITH (FORMAT CSV)').format(
                    table(name), sql.SQL(',').join(map(sql.Identifier, names)))
                log(f'Uploading production {name}')
                with gzip.open(source / f'{name}.csv.gz', 'rb') as data:
                    cur.copy_expert(command.as_string(conn), data, size=1024 * 1024)
                log(f'Uploaded production {name}')
            for ns, name, definition in indexes:
                log(f'Rebuilding index {name}')
                cur.execute(definition)
            log('Restoring and validating judge foreign key')
            cur.execute(sql.SQL('ALTER TABLE public.court_cases ADD CONSTRAINT {} {}').format(
                sql.Identifier(ref[2]), sql.SQL(ref[3])))
            log('Refreshing production judge statistics')
            refresh_stats(cur)
            actual = counts(cur)
            if actual != metadata['counts']:
                raise RuntimeError(f'Row-count mismatch: {actual} != {metadata["counts"]}')
            cur.execute('SELECT sum("TotalCasesDecided") FROM public.judges')
            if cur.fetchone()[0] != actual['decided_cases']:
                raise RuntimeError('Judge statistics do not match decided-case count')
            log(f'Validated production counts: {actual}')
        conn.commit()
        log('COMMITTED: production EOIR replacement succeeded')
    except BaseException:
        conn.rollback()
        log('ROLLBACK requested; replacement was not confirmed committed')
        raise
    with conn.cursor() as cur:
        cur.execute('ANALYZE public.judges')
        cur.execute('ANALYZE public.court_cases')
    conn.commit()
    log('Production table statistics analyzed')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--local-dsn', default='dbname=meridian user=postgres host=localhost')
    parser.add_argument('--output-dir', type=Path,
                        default=Path.home() / 'MeridianData' / 'exports' / time.strftime('eoir-%Y%m%d-%H%M%S'))
    parser.add_argument('--restore-from', type=Path,
                        help='Use an earlier export or backup directory instead of local data')
    parser.add_argument('--backup-only', action='store_true', help='Back up production without changing it')
    args = parser.parse_args()
    dsn = os.environ.get('PROD_URL') or getpass.getpass('Production PostgreSQL URL (hidden): ')
    if not dsn:
        parser.error('Set PROD_URL to the production PostgreSQL connection URL')
    os.umask(0o077)
    args.output_dir.mkdir(parents=True, exist_ok=False)
    prod = connect(dsn)
    local = None
    try:
        with prod.cursor() as cur:
            dependency_check(cur)
            prod_schema = schema(cur)
        prod.rollback()
        if args.backup_only:
            export(prod, args.output_dir / 'production-backup', 'production backup')
            return
        if args.restore_from:
            source = args.restore_from.resolve()
            metadata = json.loads((source / 'manifest.json').read_text())
        else:
            local = connect(args.local_dsn)
            with local.cursor() as cur:
                if schema(cur) != prod_schema:
                    raise RuntimeError('Local and production column schemas differ')
            local.rollback()
            source = args.output_dir / 'local-export'
            metadata = export(local, source, 'local source')
        if metadata['schema'] != prod_schema:
            raise RuntimeError('Source and production column schemas differ')
        if metadata['counts']['judges'] < 1 or metadata['counts']['court_cases'] < 1:
            raise RuntimeError('Refusing to publish an empty dataset')
        for name in TABLES:
            if not (source / f'{name}.csv.gz').is_file():
                raise RuntimeError(f'Missing source file for {name}')
        backup = args.output_dir / 'production-backup'
        export(prod, backup, 'production backup')
        log(f'Production backup complete: {backup}')
        restore(prod, source, metadata)
        (args.output_dir / 'SUCCESS.json').write_text(json.dumps(metadata['counts'], indent=2) + '\n')
        log(f'Done. Backup and completion record: {args.output_dir}')
    finally:
        prod.close()
        if local:
            local.close()


if __name__ == '__main__':
    main()
