import psycopg2
import csv
import uuid
from datetime import datetime
import io
import sys

DB = "dbname=meridian user=postgres host=localhost"

DEC_CODE_MAP = {
    'G': 0,   # GrantedRelief
    'C': 0,   # GrantedRelief
    'A': 0,   # GrantedRelief
    'S': 0,   # GrantedRelief
    'D': 1,   # DeniedRelief
    'E': 1,   # DeniedRelief
    'X': 2,   # OrderOfRemoval
    'R': 2,   # OrderOfRemoval
    'V': 3,   # VoluntaryDeparture
    'T': 4,   # Terminated
    'W': 5,   # Withdrawn
    'O': 6,   # Other
}

CASE_TYPE_MAP = {
    'RMV': 0,
    'DEP': 0,
    'CFR': 1,
    'RFR': 1,
    'WHO': 4,
    'CSR': 2,
}

STATUS_MAP = {
    'C': 1,   # Completed
    'P': 2,   # Pending
    'A': 3,   # Active
}

EOIR_PATH = "/Users/edgarmora/MeridianData/imports/EOIR Case Data"
LOOKUP_PATH = f"{EOIR_PATH}/Lookup"
CASE_PATH = f"{EOIR_PATH}/EOIR Case Data"

def parse_date(s):
    if not s or s.strip() == '':
        return None
    try:
        return datetime.strptime(s.strip()[:10], '%Y-%m-%d').strftime('%Y-%m-%d 00:00:00+00')
    except:
        return None

def safe_str(s, max_len=None):
    if not s:
        return None
    s = s.strip().replace('\x00', '')
    if max_len:
        s = s[:max_len]
    return s if s else None

print("Loading lookups...")
conn = psycopg2.connect(DB)
cur = conn.cursor()

# One transaction: any failure rolls back judges and cases together.
cur.execute("SELECT pg_advisory_xact_lock(742019)")
cur.execute('SELECT count(*) FROM court_cases')
print('Existing court cases:', cur.fetchone()[0], flush=True)
cur.execute('SELECT "CourtCode", "Id", "CourtCity", "CourtState" FROM judges ORDER BY "CreatedAt", "Id"')
judge_map = {}
for code, ident, city, state in cur.fetchall():
    if code:
        judge_map.setdefault(code.strip(), (str(ident), city, state))

def lookup(name):
    with open(f"{LOOKUP_PATH}/{name}", encoding='utf-8-sig') as f:
        yield from csv.DictReader((line.replace('\x00', '') for line in f), delimiter='\t')

court_map = {r['BASE_CITY_CODE'].strip(): (r['BASE_CITY'].strip(), r['BASE_STATE'].strip())
             for r in lookup('tblLookupBaseCity.csv')}
country_map = {}
added = 0
for r in lookup('tblLookupJudge.csv'):
    code = safe_str(r['JUDGE_CODE'])
    name = safe_str(r['JUDGE_NAME'])
    if not code or not name or code == 'AAA' or code in judge_map:
        continue
    last, _, first = name.partition(',')
    first = first.strip().rstrip('.') or last.strip().rstrip('.')
    last = last.strip().rstrip('.')
    ident = str(uuid.uuid4())
    city, state = r['JUDGE_CITY'].strip(), r['JUDGE_STATE'].strip()
    cur.execute('''INSERT INTO judges ("Id", "FirstName", "LastName", "CourtCity", "CourtState", "CourtCode", "IsActive", "TotalCasesDecided", "GrantRatePercent", "DenialRatePercent", "RemovalOrderRatePercent", "CreatedAt") VALUES (%s,%s,%s,%s,%s,%s,%s,0,0,0,0,NOW())''',
        (ident, first[:100], last[:100], city[:100], state[:2], code, r['blnActive'] == '1'))
    judge_map[code] = (ident, city, state)
    added += 1
print(f'Judge codes: {len(judge_map)}; new judges: {added}', flush=True)
cur.execute('CREATE TEMP TABLE eoir_cases_stage (LIKE court_cases INCLUDING DEFAULTS) ON COMMIT DROP')
columns = ['Id', 'CaseNumber', 'RespondentNationality', 'RespondentCustodyStatus', 'CourtCity', 'CourtState', 'JudgeId', 'CaseType', 'Status', 'ChargesOfRemovability', 'ReliefApplications', 'HearingDate', 'DecisionDate', 'FilingDate', 'Outcome', 'IsAppealed', 'AppealOutcome', 'DataSource', 'SourceRecordId', 'CreatedAt', 'UpdatedAt']
def copy_batch(buf):
    buf.seek(0)
    cols = ','.join('"' + c + '"' for c in columns)
    cur.copy_expert(f'COPY eoir_cases_stage ({cols}) FROM STDIN WITH (FORMAT CSV)', buf)

print("Importing proceedings via COPY...")
proceeding_file = f"{CASE_PATH}/B_TblProceeding.csv"

imported = 0
skipped = 0
errors = 0
batch_size = 50000
now = datetime.utcnow().strftime('%Y-%m-%d %H:%M:%S+00')

buf = io.StringIO()
writer = csv.writer(buf, quoting=csv.QUOTE_MINIMAL)

with open(proceeding_file, encoding='utf-8-sig', errors='replace') as raw:
    f = (line.replace('\x00', '') for line in raw)
    reader = csv.DictReader(f, delimiter='\t')
    
    for i, row in enumerate(reader):
        try:
            idn_proceeding = safe_str(row.get('IDNPROCEEDING'))
            idn_case = safe_str(row.get('IDNCASE'))
            ij_code = safe_str(row.get('IJ_CODE'))
            base_city = safe_str(row.get('BASE_CITY_CODE'))
            dec_code = safe_str(row.get('DEC_CODE') or row.get('DEC_TYPDEC_CODE'))
            comp_date = parse_date(row.get('COMP_DATE'))
            osc_date = parse_date(row.get('OSC_DATE'))
            hearing_date = parse_date(row.get('HEARING_DATE'))
            nat = safe_str(row.get('NAT'), 3)
            case_type_code = safe_str(row.get('CASE_TYPE'))
            custody = safe_str(row.get('CUSTODY'))

            if not idn_proceeding or not ij_code:
                skipped += 1
                continue

            judge_info = judge_map.get(ij_code)
            if not judge_info:
                skipped += 1
                continue

            judge_id, court_city, court_state = judge_info

            if not court_city and base_city:
                court_info = court_map.get(base_city)
                if court_info:
                    court_city, court_state = court_info

            if not court_city:
                skipped += 1
                continue

            case_type = CASE_TYPE_MAP.get(case_type_code, 6)
            outcome = DEC_CODE_MAP.get(dec_code) if dec_code and comp_date else None
            status = 3 if comp_date else 0

            row_id = str(uuid.uuid4())
            nationality = country_map.get(nat, nat) if nat else None
            if nationality and len(nationality) > 3:
                nationality = nationality[:3]

            writer.writerow([
                row_id,           # Id
                idn_proceeding,   # CaseNumber
                nationality,      # RespondentNationality
                custody,          # RespondentCustodyStatus
                court_city[:100], # CourtCity
                court_state[:2] if court_state else 'XX',  # CourtState
                judge_id,         # JudgeId
                case_type,        # CaseType
                status,           # Status
                None,             # ChargesOfRemovability
                None,             # ReliefApplications
                hearing_date,     # HearingDate
                comp_date,        # DecisionDate
                osc_date,         # FilingDate
                outcome,          # Outcome
                False,            # IsAppealed
                None,             # AppealOutcome
                0,                # DataSource (TRAC; existing EOIR convention)
                idn_case,         # SourceRecordId
                now,              # CreatedAt
                None,             # UpdatedAt
            ])

            imported += 1

            if imported % batch_size == 0:
                copy_batch(buf)
                buf = io.StringIO()
                writer = csv.writer(buf, quoting=csv.QUOTE_MINIMAL)
                print(f"  {imported:,} imported, {skipped:,} skipped...")

        except Exception:
            conn.rollback()
            raise

if buf.tell():
    copy_batch(buf)
print(f'Staged {imported:,}; skipped {skipped:,}. Validating unique proceeding IDs...', flush=True)
if imported == 0:
    raise RuntimeError('No cases staged')
cur.execute('CREATE UNIQUE INDEX ON eoir_cases_stage ("CaseNumber")')
cur.execute('SELECT count(*) FROM eoir_cases_stage s LEFT JOIN judges j ON j."Id"=s."JudgeId" WHERE j."Id" IS NULL')
assert cur.fetchone()[0] == 0
print('Replacing local court cases and building indexes...', flush=True)
cur.execute('TRUNCATE court_cases')
cur.execute('INSERT INTO court_cases SELECT * FROM eoir_cases_stage')
print('Refreshing judge statistics...', flush=True)
cur.execute('''UPDATE judges SET "TotalCasesDecided"=0, "GrantRatePercent"=0, "DenialRatePercent"=0, "RemovalOrderRatePercent"=0, "StatsLastRefreshed"=NOW()''')
cur.execute('''UPDATE judges j SET "TotalCasesDecided"=s.total,
"GrantRatePercent"=ROUND(s.grants*100.0/NULLIF(s.total,0),2),
"DenialRatePercent"=ROUND(s.denials*100.0/NULLIF(s.total,0),2),
"RemovalOrderRatePercent"=ROUND(s.removals*100.0/NULLIF(s.total,0),2)
FROM (SELECT "JudgeId",COUNT(*) total,
COUNT(*) FILTER (WHERE "Outcome"=0) grants,
COUNT(*) FILTER (WHERE "Outcome"=1) denials,
COUNT(*) FILTER (WHERE "Outcome"=2) removals
FROM court_cases WHERE "Outcome" IS NOT NULL GROUP BY "JudgeId") s
WHERE j."Id"=s."JudgeId"''')
cur.execute('SELECT count(*) FROM court_cases')
assert cur.fetchone()[0] == imported
conn.commit()
print(f'COMMITTED. Imported: {imported:,} | Skipped: {skipped:,} | Errors: 0', flush=True)
cur.execute('ANALYZE court_cases')
cur.execute('ANALYZE judges')
conn.commit()
conn.close()
