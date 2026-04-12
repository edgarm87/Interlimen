using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Globalization;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddInfrastructureServices(ctx.Configuration);
        services.AddLogging(b => b.AddConsole());
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var config = host.Services.GetRequiredService<IConfiguration>();
var dataPath = config["ImportSettings:DataPath"] ?? throw new Exception("DataPath not configured");
var csvPath = config["ImportSettings:CsvPath"] ?? Path.Combine(dataPath, "csv");

logger.LogInformation("Meridian Importer starting...");

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied");
}

var command = args.FirstOrDefault() ?? "help";

switch (command)
{
    case "removals":
        await ImportICERemovalsAsync(csvPath, host.Services, logger);
        break;
    case "detention":
        await ImportDetentionAsync(dataPath, host.Services, logger);
        break;
    case "ddp-arrests":
        await ImportDdpArrestsAsync(dataPath, host.Services, logger);
        break;
    case "ddp-removals":
        await ImportDdpRemovalsAsync(dataPath, host.Services, logger);
        break;
    case "ddp-detentions":
        await ImportDdpDetentionsAsync(dataPath, host.Services, logger);
        break;
    case "ddp-detainers":
        await ImportDdpDetainersAsync(dataPath, host.Services, logger);
        break;
    case "provision":
        await ProvisionTrialUserAsync(args, host.Services, logger);
        break;
    default:
        logger.LogInformation(
            "Available commands: removals, detention, ddp-arrests, ddp-removals, ddp-detentions, ddp-detainers");
        break;
}

logger.LogInformation("Meridian Importer finished.");

// ── Helpers ───────────────────────────────────────────────────────────────────

static DateTime Utc(DateTime dt) => DateTime.SpecifyKind(dt, DateTimeKind.Utc);

static DateTime? UtcOrNull(object? val)
{
    if (val is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    if (val is string s && DateTime.TryParse(s, out var parsed)) return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    return null;
}

static bool? ParseYesNo(object? val)
{
    if (val is string s)
        return s.Trim().Equals("YES", StringComparison.OrdinalIgnoreCase) ? true :
            s.Trim().Equals("NO", StringComparison.OrdinalIgnoreCase) ? false : null;
    return null;
}

static bool? ParseYesNoCol(IXLRow row, int col)
{
    var val = row.Cell(col).GetValue<string>()?.Trim();
    if (string.IsNullOrWhiteSpace(val)) return null;
    return val.Equals("YES", StringComparison.OrdinalIgnoreCase) ? true :
        val.Equals("NO", StringComparison.OrdinalIgnoreCase) ? false : null;
}

static int? ParseBirthYear(object? val)
{
    if (val is double d) return (int)d;
    if (val is int i) return i;
    if (val is string s && int.TryParse(s, out var parsed)) return parsed;
    if (val is DateTime dt) return dt.Year;
    return null;
}

static string? Str(IXLRow row, int col)
{
    var v = row.Cell(col).GetValue<string>()?.Trim();
    return string.IsNullOrWhiteSpace(v) ? null : v;
}

static bool IsRedacted(string? val) =>
    val != null && (val.StartsWith("(b)(") || val.StartsWith("b(") || val.StartsWith("B("));

// ── Find files helper ─────────────────────────────────────────────────────────

static List<string> FindXlsx(string dir, string pattern)
{
    if (!Directory.Exists(dir)) return new List<string>();
    return Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
        .Where(f => !Path.GetFileName(f).StartsWith("~$"))
        .OrderBy(f => f)
        .ToList();
}

// ── Facility upsert helper ────────────────────────────────────────────────────

static async Task<Dictionary<string, Guid>> UpsertFacilitiesAsync(
    List<(string code, string name)> facilities,
    IUnitOfWork uow,
    ILogger logger)
{
    var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

    foreach (var (code, name) in facilities.DistinctBy(x => x.code))
    {
        if (string.IsNullOrWhiteSpace(code)) continue;

        var existing = await uow.DetentionFacilities.FindAsync(f => f.AffNumber == code);
        if (existing.Any())
        {
            result[code] = existing.First().Id;
            continue;
        }

        var facility = DetentionFacility.Create(
            name: string.IsNullOrWhiteSpace(name) ? code : name,
            city: "",
            state: "",
            type: FacilityType.Other,
            affNumber: code);

        await uow.DetentionFacilities.AddAsync(facility);
        result[code] = facility.Id;
    }

    await uow.SaveChangesAsync();
    logger.LogInformation("Facility upsert complete — {Count} facilities mapped", result.Count);
    return result;
}

// ── DDP Arrests (1dec2025 only — Sep 2023–Oct 2025) ──────────────────────────

static async Task ImportDdpArrestsAsync(
    string dataPath,
    IServiceProvider services,
    ILogger logger)
{
    var arrestsDir = Path.Combine(dataPath, "ice_release_1dec2025");
    var files = FindXlsx(arrestsDir, "*Arrests*");

    if (!files.Any())
    {
        logger.LogError("No arrests files found in {Path}", arrestsDir);
        return;
    }

    logger.LogInformation("Found {Count} arrests file(s)", files.Count);
    int totalImported = 0, totalSkipped = 0, totalErrors = 0;

    foreach (var file in files)
    {
        logger.LogInformation("Processing {File}...", Path.GetFileName(file));
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        // Find header row (row 7 in Dec 2025 release)
        int headerRow = 7;
        var batch = new List<EnforcementAction>();
        const int batchSize = 500;
        int imported = 0, skipped = 0, errors = 0;

        // Build column map from header row
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= 30; col++)
        {
            var h = ws.Cell(headerRow, col).GetValue<string>()?.Trim();
            if (!string.IsNullOrWhiteSpace(h)) headers[h] = col;
        }

        logger.LogInformation("Headers found: {Headers}", string.Join(", ", headers.Keys));

        for (int row = headerRow + 1; row <= lastRow; row++)
        {
            try
            {
                var appDateVal = ws.Cell(row, headers.GetValueOrDefault("Apprehension Date", 1)).Value;
                if (appDateVal.IsBlank)
                {
                    skipped++;
                    continue;
                }

                DateTime appDate;
                if (appDateVal.IsDateTime) appDate = Utc(appDateVal.GetDateTime());
                else if (DateTime.TryParse(appDateVal.ToString(), out var pd)) appDate = Utc(pd);
                else
                {
                    skipped++;
                    continue;
                }

                var nationality = headers.TryGetValue("Citizenship Country", out var cc)
                    ? Str(ws.Row(row), cc)
                    : null;
                var aor = headers.TryGetValue("Apprehension AOR", out var aorCol)
                    ? Str(ws.Row(row), aorCol)
                    : null;
                var method = headers.TryGetValue("Apprehension Method", out var mCol)
                    ? Str(ws.Row(row), mCol)
                    : null;
                var criminality = headers.TryGetValue("Apprehension Criminality", out var crCol)
                    ? Str(ws.Row(row), crCol)
                    : null;
                var uniqueId = headers.TryGetValue("Unique Identifier", out var uiCol)
                    ? Str(ws.Row(row), uiCol)
                    : null;
                var caseStatus = headers.TryGetValue("Case Status", out var csCol)
                    ? Str(ws.Row(row), csCol)
                    : null;

                var hasCriminal = criminality != null && (
                    criminality.StartsWith("1 ") ||
                    criminality.StartsWith("2 ") ||
                    criminality.Contains("Criminal") ||
                    criminality.Contains("Convicted"));

                var action = EnforcementAction.Create(
                    type: EnforcementActionType.Arrest,
                    actionDate: appDate,
                    dataSource: DataSource.DeportationDataProject,
                    nationality: nationality,
                    areaOfResponsibility: aor,
                    apprehensionMethod: method,
                    hasCriminalConviction: hasCriminal,
                    uniqueIdentifier: uniqueId);

                batch.Add(action);

                if (batch.Count >= batchSize)
                {
                    await ctx.EnforcementActions.AddRangeAsync(batch);
                    await ctx.SaveChangesAsync();
                    imported += batch.Count;
                    batch.Clear();
                    if (imported % 50000 == 0)
                        logger.LogInformation("  Progress: {Count:N0} arrests...", imported);
                }
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3) logger.LogWarning("Row {Row} error: {Msg}", row, ex.Message);
            }
        }

        if (batch.Any())
        {
            await ctx.EnforcementActions.AddRangeAsync(batch);
            await ctx.SaveChangesAsync();
            imported += batch.Count;
        }

        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;
        logger.LogInformation("{File} — Imported: {I}, Skipped: {S}, Errors: {E}",
            Path.GetFileName(file), imported, skipped, errors);
    }

    logger.LogInformation("DDP Arrests complete — Total: {T:N0}, Skipped: {S:N0}, Errors: {E:N0}",
        totalImported, totalSkipped, totalErrors);
}

// ── DDP Removals (ICFO_42034 FY2012–FY2023) ───────────────────────────────────

static async Task ImportDdpRemovalsAsync(
    string dataPath,
    IServiceProvider services,
    ILogger logger)
{
    var removalsDir = Path.Combine(dataPath, "ice_release_2023_ICFO_42034");
    var files = FindXlsx(removalsDir, "*Removals*");

    if (!files.Any())
    {
        logger.LogError("No removals files found in {Path}", removalsDir);
        return;
    }

    logger.LogInformation("Found {Count} removals file(s)", files.Count);
    int totalImported = 0, totalSkipped = 0, totalErrors = 0;

    foreach (var file in files)
    {
        logger.LogInformation("Processing {File}...", Path.GetFileName(file));
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        // Headers on row 6 for ICFO_42034
        // Columns: Departure Date, Port of Departure, Departure Country,
        //          Case Status, Case Category, Final Order Yes No, Final Order Date, Case ID
        const int headerRow = 6;
        var batch = new List<EnforcementAction>();
        const int batchSize = 500;
        int imported = 0, skipped = 0, errors = 0;

        for (int row = headerRow + 1; row <= lastRow; row++)
        {
            try
            {
                var departureDateVal = ws.Cell(row, 1).Value;
                if (departureDateVal.IsBlank)
                {
                    skipped++;
                    continue;
                }

                DateTime departureDate;
                if (departureDateVal.IsDateTime) departureDate = Utc(departureDateVal.GetDateTime());
                else if (DateTime.TryParse(departureDateVal.ToString(), out var pd)) departureDate = Utc(pd);
                else
                {
                    skipped++;
                    continue;
                }

                var portOfDeparture = Str(ws.Row(row), 2);
                var country = Str(ws.Row(row), 3);
                if (string.IsNullOrWhiteSpace(country)) { skipped++; continue; }

                var caseStatus = Str(ws.Row(row), 4);
                var caseCategory = Str(ws.Row(row), 5);
                var finalOrderStr = Str(ws.Row(row), 6);

                // Derive criminality from case status
                // "6-Deported/Removed - Deportability" = criminal grounds
                // "8-Excluded/Removed - Inadmissibility" = non-criminal
                var hasCriminal = caseStatus != null && (
                    caseStatus.StartsWith("6-") ||
                    caseStatus.StartsWith("5-") ||
                    caseStatus.StartsWith("7-"));

                var action = EnforcementAction.Create(
                    type: EnforcementActionType.Removal,
                    actionDate: departureDate,
                    dataSource: DataSource.DeportationDataProject,
                    nationality: country,
                    areaOfResponsibility: portOfDeparture,
                    dispositionCode: caseCategory,
                    hasCriminalConviction: hasCriminal);

                batch.Add(action);

                if (batch.Count >= batchSize)
                {
                    await ctx.EnforcementActions.AddRangeAsync(batch);
                    await ctx.SaveChangesAsync();
                    imported += batch.Count;
                    batch.Clear();
                    if (imported % 50000 == 0)
                        logger.LogInformation("  Progress: {Count:N0} removals...", imported);
                }
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3) logger.LogWarning("Row {Row} error: {Msg}", row, ex.Message);
            }
        }

        if (batch.Any())
        {
            await ctx.EnforcementActions.AddRangeAsync(batch);
            await ctx.SaveChangesAsync();
            imported += batch.Count;
        }

        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;
        logger.LogInformation("{File} — Imported: {I}, Skipped: {S}, Errors: {E}",
            Path.GetFileName(file), imported, skipped, errors);
    }

    logger.LogInformation("DDP Removals complete — Total: {T:N0}, Skipped: {S:N0}, Errors: {E:N0}",
        totalImported, totalSkipped, totalErrors);
}

// ── DDP Detentions (ICFO_42034 FY2012–FY2023 + 1dec2025) ─────────────────────

static async Task ImportDdpDetentionsAsync(
    string dataPath,
    IServiceProvider services,
    ILogger logger)
{
    // Collect files from both releases
    var files = new List<string>();
    files.AddRange(FindXlsx(Path.Combine(dataPath, "ice_release_2023_ICFO_42034"), "*Detention*"));
    files.AddRange(FindXlsx(Path.Combine(dataPath, "ice_release_1dec2025"), "*Detention*"));

    if (!files.Any())
    {
        logger.LogError("No detention files found in {Path}", dataPath);
        return;
    }

    logger.LogInformation("Found {Count} detention file(s)", files.Count);

    // ── Pass 1: collect all facility codes across all files ───────────────────
    logger.LogInformation("Pass 1: collecting facility codes...");
    var facilityPairs = new List<(string code, string name)>();

    foreach (var file in files)
    {
        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        int headerRow = DetectHeaderRow(ws);

        for (int row = headerRow + 1; row <= lastRow; row++)
        {
            var isOldFile = !Path.GetFileName(file).StartsWith("ICE Detentions");
            var name = Str(ws.Row(row), isOldFile ? 6 : 3);
            var code = Str(ws.Row(row), isOldFile ? 7 : 4);
            if (!string.IsNullOrWhiteSpace(code))
                facilityPairs.Add((code, name ?? code));
        }
    }

    using var facilityScope = services.CreateScope();
    var facilityUow = facilityScope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var facilityMap = await UpsertFacilitiesAsync(facilityPairs, facilityUow, logger);

    // ── Pass 2: import stints ─────────────────────────────────────────────────
    int totalImported = 0, totalSkipped = 0, totalErrors = 0;

    foreach (var file in files)
    {
        logger.LogInformation("Processing {File}...", Path.GetFileName(file));
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        int headerRow = DetectHeaderRow(ws);

        // Detect schema — ICFO_42034 vs 1dec2025 differ slightly
        var isNewRelease = Path.GetFileName(file).StartsWith("ICE Detentions");


        // ICFO_42034 schema: Detention ID(1), Case ID(2), Subject ID(3), 
        //   Stay Book In Date(4), Facility Book In Date(5), Facility Name(6), 
        //   Facility Code(7), Book Out Date(8), Stay Book Out Date(9), Release Reason(10)
        // 1dec2025 schema: Stay Book In(1), Facility Book In(2), Facility Name(3),
        //   Facility Code(4), Book Out(5), Stay Book Out(6), Release Reason(7)

        int colStayIn = isNewRelease ? 1 : 4;
        int colFacilityIn = isNewRelease ? 2 : 5;
        int colFacilityName = isNewRelease ? 3 : 6;
        int colFacilityCode = isNewRelease ? 4 : 7;
        int colBookOut = isNewRelease ? 5 : 8;
        int colStayOut = isNewRelease ? 6 : 9;
        int colRelease = isNewRelease ? 7 : 10;

        var batch = new List<DetentionStay>();
        const int batchSize = 500;
        int imported = 0, skipped = 0, errors = 0;

        for (int row = headerRow + 1; row <= lastRow; row++)
        {
            try
            {
                // Col 1: Stay Book In Date
                // Col 2: Facility Book In Date  
                // Col 3: Detention Facility (name)
                // Col 4: Detention Facility Code
                // Col 5: Detention Book Out Date
                // Col 6: Stay Book Out Date
                // Col 7: Release Reason (facility) — or "Unknown"
                // Col 8: Stay Book Out Date (date only) — new release only

                var stayInVal = ws.Cell(row, colStayIn).Value;
                var bookInVal = ws.Cell(row, colFacilityIn).Value;
                var facilityCode = Str(ws.Row(row), colFacilityCode);

                if (stayInVal.IsBlank || string.IsNullOrWhiteSpace(facilityCode))
                {
                    skipped++;
                    continue;
                }

                if (!facilityMap.TryGetValue(facilityCode, out var facilityId))
                {
                    skipped++;
                    continue;
                }

                DateTime stayBookIn;
                if (stayInVal.IsDateTime) stayBookIn = Utc(stayInVal.GetDateTime());
                else if (DateTime.TryParse(stayInVal.ToString(), out var pd)) stayBookIn = Utc(pd);
                else
                {
                    skipped++;
                    continue;
                }

                DateTime facilityBookIn;
                if (bookInVal.IsDateTime) facilityBookIn = Utc(bookInVal.GetDateTime());
                else if (DateTime.TryParse(bookInVal.ToString(), out var pd2)) facilityBookIn = Utc(pd2);
                else facilityBookIn = stayBookIn;

                var bookOutVal = ws.Cell(row, colBookOut).Value;
                var stayOutVal = ws.Cell(row, colStayOut).Value;
                var releaseReason = Str(ws.Row(row), colRelease);
                if (releaseReason == "Unknown") releaseReason = null;

                // New release has more columns — citizenship, gender, etc.
                string? citizenship = null,
                    gender = null,
                    caseStatus = null,
                    caseCategory = null,
                    caseThreatLevel = null,
                    bookInCriminality = null,
                    uniqueId = null,
                    stayReleaseReason = null;
                int? birthYear = null;
                bool? hasFinalOrder = null;

                if (isNewRelease)
                {
                    // Col 9: Stay Release Reason
                    // Col 11: Gender
                    // Col 14: Birth Year
                    // Col 20: Case Status
                    // Col 21: Case Category
                    // Col 22: Final Order Yes No
                    // Col 24: Case Threat Level
                    // Col 25: Book In Criminality
                    // Col 30: Citizenship Country
                    // Col 37: Unique Identifier
                    stayReleaseReason = Str(ws.Row(row), 9);
                    gender = Str(ws.Row(row), 11);
                    birthYear = ParseBirthYear(ws.Cell(row, 14).Value.ToString());
                    caseStatus = Str(ws.Row(row), 20);
                    caseCategory = Str(ws.Row(row), 21);
                    hasFinalOrder = ParseYesNo(ws.Cell(row, 22).GetValue<string>());
                    caseThreatLevel = Str(ws.Row(row), 24);
                    bookInCriminality = Str(ws.Row(row), 25);
                    citizenship = Str(ws.Row(row), 30);
                    uniqueId = Str(ws.Row(row), 37);
                }

                var stay = DetentionStay.Create(
                    facilityId: facilityId,
                    stayBookInDate: stayBookIn,
                    facilityBookInDate: facilityBookIn,
                    dataSource: DataSource.DeportationDataProject,
                    facilityBookOutDate: UtcOrNull(bookOutVal.IsDateTime ? bookOutVal.GetDateTime() : null),
                    stayBookOutDate: UtcOrNull(stayOutVal.IsDateTime ? stayOutVal.GetDateTime() : null),
                    facilityReleaseReason: releaseReason,
                    stayReleaseReason: stayReleaseReason,
                    caseStatus: caseStatus,
                    caseCategory: caseCategory,
                    hasFinalOrder: hasFinalOrder,
                    caseThreatLevel: caseThreatLevel,
                    bookInCriminality: bookInCriminality,
                    citizenshipCountry: citizenship,
                    gender: gender,
                    birthYear: birthYear,
                    uniqueIdentifier: uniqueId);

                batch.Add(stay);

                if (batch.Count >= batchSize)
                {
                    await ctx.DetentionStays.AddRangeAsync(batch);
                    await ctx.SaveChangesAsync();
                    imported += batch.Count;
                    batch.Clear();
                    if (imported % 50000 == 0)
                        logger.LogInformation("  Progress: {Count:N0} stints...", imported);
                }
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3) logger.LogWarning("Row {Row} error: {Msg}", row, ex.Message);
            }
        }

        if (batch.Any())
        {
            await ctx.DetentionStays.AddRangeAsync(batch);
            await ctx.SaveChangesAsync();
            imported += batch.Count;
        }

        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;
        logger.LogInformation("{File} — Imported: {I:N0}, Skipped: {S:N0}, Errors: {E:N0}",
            Path.GetFileName(file), imported, skipped, errors);
    }

    logger.LogInformation("DDP Detentions complete — Total: {T:N0}, Skipped: {S:N0}, Errors: {E:N0}",
        totalImported, totalSkipped, totalErrors);
}

// ── DDP Detainers (1dec2025 only) ─────────────────────────────────────────────

static async Task ImportDdpDetainersAsync(
    string dataPath,
    IServiceProvider services,
    ILogger logger)
{
    var detainersDir = Path.Combine(dataPath, "ice_release_1dec2025");
    var files = FindXlsx(detainersDir, "*Detainer*");

    if (!files.Any())
    {
        logger.LogError("No detainers files found in {Path}", detainersDir);
        return;
    }

    logger.LogInformation("Found {Count} detainers file(s)", files.Count);
    int totalImported = 0, totalSkipped = 0, totalErrors = 0;

    foreach (var file in files)
    {
        logger.LogInformation("Processing {File}...", Path.GetFileName(file));
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        using var wb = new XLWorkbook(file);
        var ws = wb.Worksheet(1);
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        // Headers on row 7 for 1dec2025 release
        const int headerRow = 7;
        var batch = new List<IceDetainer>();
        const int batchSize = 500;
        int imported = 0, skipped = 0, errors = 0;

        // Build column map
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= 70; col++)
        {
            var h = ws.Cell(headerRow, col).GetValue<string>()?.Trim();
            if (!string.IsNullOrWhiteSpace(h)) headers[h] = col;
        }

        int Col(string name) => headers.TryGetValue(name, out var c) ? c : -1;

        for (int row = headerRow + 1; row <= lastRow; row++)
        {
            try
            {
                var prepDateVal = ws.Cell(row, Col("Detainer Prepare Date")).Value;
                if (prepDateVal.IsBlank)
                {
                    skipped++;
                    continue;
                }

                DateTime prepDate;
                if (prepDateVal.IsDateTime) prepDate = Utc(prepDateVal.GetDateTime());
                else if (DateTime.TryParse(prepDateVal.ToString(), out var pd)) prepDate = Utc(pd);
                else
                {
                    skipped++;
                    continue;
                }

                var detainer = IceDetainer.Create(
                    detainerPrepareDate: prepDate,
                    dataSource: DataSource.DeportationDataProject,
                    facilityState: Col("Facility State") > 0 ? Str(ws.Row(row), Col("Facility State")) : null,
                    facilityAor: Col("Facility AOR") > 0 ? Str(ws.Row(row), Col("Facility AOR")) : null,
                    detentionFacilityName: Col("Detention Facility") > 0
                        ? Str(ws.Row(row), Col("Detention Facility"))
                        : null,
                    detentionFacilityCode: Col("Detention Facility Code") > 0
                        ? Str(ws.Row(row), Col("Detention Facility Code"))
                        : null,
                    facilityCity: Col("Facility City") > 0 ? Str(ws.Row(row), Col("Facility City")) : null,
                    portOfDeparture: Col("Port of Departure") > 0 ? Str(ws.Row(row), Col("Port of Departure")) : null,
                    departureCountry: Col("Departure Country") > 0 ? Str(ws.Row(row), Col("Departure Country")) : null,
                    departedDate: Col("Departed Date") > 0
                        ? UtcOrNull(ws.Cell(row, Col("Departed Date")).Value.IsDateTime
                            ? ws.Cell(row, Col("Departed Date")).GetDateTime()
                            : null)
                        : null,
                    caseStatus: Col("Case Status") > 0 ? Str(ws.Row(row), Col("Case Status")) : null,
                    caseCategory: Col("Case Category") > 0 ? Str(ws.Row(row), Col("Case Category")) : null,
                    detainerCriminality: Col("Detainer Prepared Criminality") > 0
                        ? Str(ws.Row(row), Col("Detainer Prepared Criminality"))
                        : null,
                    threatLevel: Col("Detainer Prep Threat Level") > 0
                        ? Str(ws.Row(row), Col("Detainer Prep Threat Level"))
                        : null,
                    processingDisposition: Col("Processing Disposition") > 0
                        ? Str(ws.Row(row), Col("Processing Disposition"))
                        : null,
                    finalProgram: Col("Final Program") > 0 ? Str(ws.Row(row), Col("Final Program")) : null,
                    hasFinalOrder: Col("Case Final Order Yes No") > 0
                        ? ParseYesNo(ws.Cell(row, Col("Case Final Order Yes No")).GetValue<string>())
                        : null,
                    finalOrderDate: Col("Final Order Date") > 0
                        ? UtcOrNull(ws.Cell(row, Col("Final Order Date")).Value.IsDateTime
                            ? ws.Cell(row, Col("Final Order Date")).GetDateTime()
                            : null)
                        : null,
                    apprehensionDate: Col("Apprehension Date") > 0
                        ? UtcOrNull(ws.Cell(row, Col("Apprehension Date")).Value.IsDateTime
                            ? ws.Cell(row, Col("Apprehension Date")).GetDateTime()
                            : null)
                        : null,
                    citizenshipCountry: Col("Citizenship Country") > 0
                        ? Str(ws.Row(row), Col("Citizenship Country"))
                        : null,
                    birthCountry: Col("Birth Country") > 0 ? Str(ws.Row(row), Col("Birth Country")) : null,
                    birthYear: Col("Birth Year") > 0
                        ? ParseBirthYear(ws.Cell(row, Col("Birth Year")).Value.ToString())
                        : null,
                    gender: Col("Gender") > 0 ? Str(ws.Row(row), Col("Gender")) : null,
                    entryStatus: Col("Entry Status") > 0 ? Str(ws.Row(row), Col("Entry Status")) : null,
                    isFelon: Col("Felon") > 0 ? ParseYesNo(ws.Cell(row, Col("Felon")).GetValue<string>()) : null,
                    priorFelony: Col("Prior Felony Yes No") > 0
                        ? ParseYesNo(ws.Cell(row, Col("Prior Felony Yes No")).GetValue<string>())
                        : null,
                    aggravatedFelony: Col("Aggravated Felony Yes No") > 0
                        ? ParseYesNo(ws.Cell(row, Col("Aggravated Felony Yes No")).GetValue<string>())
                        : null,
                    violentMisdemeanor: Col("Violent Misdemeanor Yes No") > 0
                        ? ParseYesNo(ws.Cell(row, Col("Violent Misdemeanor Yes No")).GetValue<string>())
                        : null,
                    mostSeriousConvictionCharge: Col("Most Serious Conviction (MSC) Charge") > 0
                        ? Str(ws.Row(row), Col("Most Serious Conviction (MSC) Charge"))
                        : null,
                    detainerType: Col("Detainer Type") > 0 ? Str(ws.Row(row), Col("Detainer Type")) : null,
                    detainerLiftReason: Col("Detainer Lift Reason") > 0
                        ? Str(ws.Row(row), Col("Detainer Lift Reason"))
                        : null,
                    uniqueIdentifier: Col("Unique Identifier") > 0 ? Str(ws.Row(row), Col("Unique Identifier")) : null);

                batch.Add(detainer);

                if (batch.Count >= batchSize)
                {
                    await ctx.IceDetainers.AddRangeAsync(batch);
                    await ctx.SaveChangesAsync();
                    imported += batch.Count;
                    batch.Clear();
                    if (imported % 50000 == 0)
                        logger.LogInformation("  Progress: {Count:N0} detainers...", imported);
                }
            }
            catch (Exception ex)
            {
                errors++;
                if (errors <= 3) logger.LogWarning("Row {Row} error: {Msg}", row, ex.Message);
            }
        }

        if (batch.Any())
        {
            await ctx.IceDetainers.AddRangeAsync(batch);
            await ctx.SaveChangesAsync();
            imported += batch.Count;
        }

        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;
        logger.LogInformation("{File} — Imported: {I:N0}, Skipped: {S:N0}, Errors: {E:N0}",
            Path.GetFileName(file), imported, skipped, errors);
    }

    logger.LogInformation("DDP Detainers complete — Total: {T:N0}, Skipped: {S:N0}, Errors: {E:N0}",
        totalImported, totalSkipped, totalErrors);
}

// ── Header row detector ───────────────────────────────────────────────────────

static int DetectHeaderRow(IXLWorksheet ws)
{
    for (int row = 1; row <= 15; row++)
    {
        var cell = ws.Cell(row, 1).GetValue<string>()?.Trim();
        if (!string.IsNullOrWhiteSpace(cell) &&
            (cell.Contains("Stay Book") || cell.Contains("Detention ID") ||
             cell.Contains("Book In") || cell.Contains("Apprehension")))
            return row;
    }

    return 6; // fallback
}

// ── Legacy importers (kept intact) ───────────────────────────────────────────

static async Task ImportICERemovalsAsync(
    string csvPath,
    IServiceProvider services,
    ILogger logger)
{
    var files = Directory.GetFiles(csvPath, "ice_removals_*.csv")
        .OrderBy(f => f).ToList();

    if (!files.Any())
    {
        logger.LogError("No ICE removal CSV files found in {Path}", csvPath);
        return;
    }

    logger.LogInformation("Found {Count} CSV files to import", files.Count);
    int totalImported = 0, totalSkipped = 0, totalErrors = 0;

    foreach (var file in files)
    {
        logger.LogInformation("Processing {File}...", Path.GetFileName(file));
        using var scope = services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var (imported, skipped, errors) = await ImportSingleCsvFileAsync(file, uow, logger);
        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;
        logger.LogInformation("{File} done — Imported: {I}, Skipped: {S}, Errors: {E}",
            Path.GetFileName(file), imported, skipped, errors);
    }

    logger.LogInformation("All files complete — Total: {T}, Skipped: {S}, Errors: {E}",
        totalImported, totalSkipped, totalErrors);
}

static async Task<(int imported, int skipped, int errors)> ImportSingleCsvFileAsync(
    string filePath, IUnitOfWork uow, ILogger logger)
{
    int imported = 0, skipped = 0, errors = 0;
    var batch = new List<EnforcementAction>();
    const int batchSize = 500;

    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null,
        TrimOptions = TrimOptions.Trim
    };

    using var reader = new StreamReader(filePath);
    using var csv = new CsvReader(reader, csvConfig);
    await csv.ReadAsync();
    csv.ReadHeader();

    while (await csv.ReadAsync())
    {
        try
        {
            var removalDateStr = csv.GetField("Removal Date") ?? csv.GetField("Departed Date");
            var country = csv.GetField("Citizenship Country")?.Trim();
            var aor = csv.GetField("Area of Responsibility")?.Trim();
            var appMethod = csv.GetField("Apprehension Method Code")?.Trim();
            var dispositionCode = csv.GetField("Processing Disposition Code")?.Trim();
            var chargeSection = csv.GetField("Final Charge Section")?.Trim();

            if (string.IsNullOrWhiteSpace(country))
            {
                skipped++;
                continue;
            }

            if (!DateTime.TryParse(removalDateStr, out var removalDate))
            {
                skipped++;
                continue;
            }

            removalDate = DateTime.SpecifyKind(removalDate, DateTimeKind.Utc);

            var hasCriminal = !string.IsNullOrWhiteSpace(chargeSection) &&
                              !chargeSection.StartsWith("(b)") &&
                              (chargeSection.StartsWith("237a2") || chargeSection.StartsWith("212a2"));

            var action = EnforcementAction.Create(
                type: EnforcementActionType.Removal,
                actionDate: removalDate,
                dataSource: DataSource.ICE_ERO,
                nationality: country,
                areaOfResponsibility: aor,
                apprehensionMethod: appMethod,
                dispositionCode: dispositionCode,
                hasCriminalConviction: hasCriminal,
                mostSeriousOffense: string.IsNullOrWhiteSpace(chargeSection) ||
                                    chargeSection.StartsWith("(b)")
                    ? null
                    : chargeSection);

            batch.Add(action);

            if (batch.Count >= batchSize)
            {
                foreach (var item in batch) await uow.EnforcementActions.AddAsync(item);
                await uow.SaveChangesAsync();
                imported += batch.Count;
                batch.Clear();
            }
        }
        catch (Exception ex)
        {
            errors++;
            if (errors <= 3) logger.LogWarning("Row error: {Message}", ex.Message);
        }
    }

    if (batch.Any())
    {
        foreach (var item in batch) await uow.EnforcementActions.AddAsync(item);
        await uow.SaveChangesAsync();
        imported += batch.Count;
    }

    return (imported, skipped, errors);
}

static async Task ImportDetentionAsync(
    string dataPath,
    IServiceProvider services,
    ILogger logger)
{
    var file = Directory.GetFiles(dataPath, "detention-facility-daily-population*.xlsx")
        .FirstOrDefault();

    if (file == null)
    {
        logger.LogError("No detention facility file found in {Path}", dataPath);
        return;
    }

    logger.LogInformation("Importing detention data from {File}", Path.GetFileName(file));

    using var workbook = new XLWorkbook(file);
    var sheet = workbook.Worksheet("data");
    var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

    logger.LogInformation("Found {Rows} rows to process", lastRow - 1);

    using var scope = services.CreateScope();
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    var facilities = new Dictionary<string, Guid>();

    for (int row = 2; row <= lastRow; row++)
    {
        var code = sheet.Cell(row, 1).GetValue<string>()?.Trim();
        var name = sheet.Cell(row, 2).GetValue<string>()?.Trim();
        var state = sheet.Cell(row, 3).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(code) || facilities.ContainsKey(code)) continue;

        var existing = await uow.DetentionFacilities.FindAsync(f => f.AffNumber == code);
        if (existing.Any())
        {
            facilities[code] = existing.First().Id;
            continue;
        }

        var facility = DetentionFacility.Create(
            name: name ?? code, city: state ?? "", state: state ?? "",
            type: FacilityType.IGSA, affNumber: code);

        await uow.DetentionFacilities.AddAsync(facility);
        facilities[code] = facility.Id;
    }

    await uow.SaveChangesAsync();
    logger.LogInformation("Created {Count} facilities", facilities.Count);

    int imported = 0, skipped = 0;
    var batch = new List<DetentionPopulationSnapshot>();
    const int batchSize = 500;

    for (int row = 2; row <= lastRow; row++)
    {
        var code = sheet.Cell(row, 1).GetValue<string>()?.Trim();
        var dateStr = sheet.Cell(row, 4).GetValue<string>()?.Trim();
        var nDetained = sheet.Cell(row, 5).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(code) || !facilities.ContainsKey(code))
        {
            skipped++;
            continue;
        }

        if (!DateTime.TryParse(dateStr, out var date))
        {
            skipped++;
            continue;
        }

        if (!int.TryParse(nDetained, out var totalDetained))
        {
            skipped++;
            continue;
        }

        var snapshot = DetentionPopulationSnapshot.Create(
            facilityId: facilities[code],
            snapshotDate: DateTime.SpecifyKind(date, DateTimeKind.Utc),
            totalDetained: totalDetained,
            dataSource: DataSource.DeportationDataProject);

        batch.Add(snapshot);

        if (batch.Count >= batchSize)
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ctx.DetentionSnapshots.AddRangeAsync(batch);
            await ctx.SaveChangesAsync();
            imported += batch.Count;
            batch.Clear();
        }
    }

    if (batch.Any())
    {
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ctx.DetentionSnapshots.AddRangeAsync(batch);
        await ctx.SaveChangesAsync();
        imported += batch.Count;
    }

    logger.LogInformation("Detention import complete — Facilities: {F}, Snapshots: {S}, Skipped: {Sk}",
        facilities.Count, imported, skipped);
}

static async Task ProvisionTrialUserAsync(
    string[] args,
    IServiceProvider services,
    ILogger logger)
{
    // Usage: provision "Firm Name" "First" "Last" "email@firm.com" "Password123!"
    if (args.Length < 6)
    {
        logger.LogError("Usage: provision \"Org Name\" \"First\" \"Last\" \"email\" \"password\"");
        return;
    }

    var orgName   = args[1];
    var firstName = args[2];
    var lastName  = args[3];
    var email     = args[4];
    var password  = args[5];

    using var scope = services.CreateScope();
    var uow = scope.ServiceProvider
        .GetRequiredService<IUnitOfWork>();
    var userManager = scope.ServiceProvider
        .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<Meridian.Infrastructure.Identity.AppUser>>();

    var existing = await userManager.FindByEmailAsync(email);
    if (existing != null)
    {
        logger.LogError("User {Email} already exists", email);
        return;
    }

    var slug = System.Text.RegularExpressions.Regex
        .Replace(orgName.ToLower().Trim(), @"[^a-z0-9]+", "-")
        .Trim('-');

    var tenant = Tenant.Create(orgName, slug, SubscriptionTier.Pro, 3);
    tenant.StartTrial(14);
    await uow.Tenants.AddAsync(tenant);
    await uow.SaveChangesAsync();

    var user = new Meridian.Infrastructure.Identity.AppUser
    {
        UserName = email,
        Email = email,
        FirstName = firstName,
        LastName = lastName,
        TenantId = tenant.Id,
        Role = Meridian.Infrastructure.Identity.UserRole.Owner,
        EmailConfirmed = true
    };

    var result = await userManager.CreateAsync(user, password);

    if (!result.Succeeded)
    {
        logger.LogError("Failed: {Errors}",
            string.Join(", ", result.Errors.Select(e => e.Description)));
        return;
    }

    logger.LogInformation(
        "Trial account created — {Email} / {OrgName} / expires {Expiry:yyyy-MM-dd}",
        email, orgName, DateTime.UtcNow.AddDays(14));
}