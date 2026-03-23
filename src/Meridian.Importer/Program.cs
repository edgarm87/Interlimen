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
        services.AddLogging(b => b
            .AddConsole()
            .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning));
    })
    .Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var config = host.Services.GetRequiredService<IConfiguration>();
var dataPath = config["ImportSettings:DataPath"] ?? throw new Exception("DataPath not configured");

logger.LogInformation("Meridian Importer starting...");
logger.LogInformation("Data path: {DataPath}", dataPath);

// Run migrations first
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied");
}

// Import OHSS Table 42 — Removals by nationality
var ohssFile = Path.Combine(dataPath, "ohss_table42_fy2022.xlsx");
if (File.Exists(ohssFile))
{
    logger.LogInformation("Importing OHSS Table 42 from {File}", ohssFile);
    using var scope = host.Services.CreateScope();
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var result = await ImportOHSSTable42Async(ohssFile, uow, logger);
    logger.LogInformation("OHSS Import complete — Imported: {Imported}, Skipped: {Skipped}",
        result.imported, result.skipped);
}

// Import ICE individual-level removal records
var csvDir = Path.Combine(dataPath, "csv");
if (Directory.Exists(csvDir))
{
    var csvFiles = Directory.GetFiles(csvDir, "ice_removals_fy*.csv")
        .OrderBy(f => f)
        .ToArray();

    logger.LogInformation("Found {Count} ICE removal CSV files to import", csvFiles.Length);

    foreach (var csvFile in csvFiles)
    {
        var fileName = Path.GetFileName(csvFile);
        logger.LogInformation("Importing {File}...", fileName);

        using var scope = host.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var (imported, skipped) = await ImportICERemovalsCsvAsync(csvFile, uow, logger);

        logger.LogInformation("{File} complete — Imported: {Imported:N0}, Skipped: {Skipped:N0}",
            fileName, imported, skipped);
    }
}

logger.LogInformation("Meridian Importer finished.");

// ── OHSS Importer ─────────────────────────────────────────────────────────────

static async Task<(int imported, int skipped, int errors)> ImportOHSSTable42Async(
    string filePath,
    IUnitOfWork uow,
    ILogger logger)
{
    int imported = 0, skipped = 0, errors = 0;

    var yearColumns = new Dictionary<int, int>
    {
        { 2013, 2 }, { 2014, 5 }, { 2015, 8 }, { 2016, 11 },
        { 2017, 14 }, { 2018, 17 }, { 2019, 20 }, { 2020, 23 },
        { 2021, 26 }, { 2022, 29 }
    };

    var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "REGION", "COUNTRY", "Total", "Africa", "Asia", "Europe",
        "North America", "Oceania", "South America", "Unknown", ""
    };

    var queued = new HashSet<string>();

    var existing = await uow.RemovalStatistics.GetAllAsync();
    foreach (var e in existing)
        await uow.RemovalStatistics.DeleteAsync(e);
    await uow.SaveChangesAsync();
    logger.LogInformation("Cleared {Count} existing OHSS records", existing.Count);

    using var workbook = new XLWorkbook(filePath);
    var sheet = workbook.Worksheet("Table 42d");
    var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

    for (int row = 9; row <= lastRow; row++)
    {
        var countryName = sheet.Cell(row, 1).GetValue<string>()?.Trim();

        if (string.IsNullOrWhiteSpace(countryName)) continue;
        if (skipNames.Contains(countryName)) continue;
        if (countryName.StartsWith("D ") || countryName.StartsWith("1 ") ||
            countryName.StartsWith("2 ") || countryName.StartsWith("3 ") ||
            countryName.StartsWith("Source:")) continue;

        foreach (var (year, startCol) in yearColumns)
        {
            var key = $"{countryName}|{year}";
            if (queued.Contains(key)) { skipped++; continue; }

            var totalStr = sheet.Cell(row, startCol).GetValue<string>();
            var criminalStr = sheet.Cell(row, startCol + 1).GetValue<string>();
            var nonCriminalStr = sheet.Cell(row, startCol + 2).GetValue<string>();

            if (!TryParseCount(totalStr, out var total)) { skipped++; continue; }
            TryParseCount(criminalStr, out var criminal);
            TryParseCount(nonCriminalStr, out var nonCriminal);

            try
            {
                var stat = RemovalStatistic.Create(
                    year: year,
                    month: 0,
                    nationality: GenerateCode(countryName),
                    nationalityDisplay: countryName,
                    totalRemovals: total,
                    criminalRemovals: criminal,
                    nonCriminalRemovals: nonCriminal,
                    dataSource: DataSource.OHSS
                );

                await uow.RemovalStatistics.AddAsync(stat);
                queued.Add(key);
                imported++;

                if (imported % 200 == 0)
                {
                    await uow.SaveChangesAsync();
                    logger.LogInformation("OHSS Progress: {Count} records...", imported);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error importing {Country} {Year}", countryName, year);
                errors++;
            }
        }
    }

    await uow.SaveChangesAsync();
    return (imported, skipped, errors);
}

// ── ICE CSV Importer ──────────────────────────────────────────────────────────

static async Task<(int imported, int skipped)> ImportICERemovalsCsvAsync(
    string filePath,
    IUnitOfWork uow,
    ILogger logger)
{
    int imported = 0, skipped = 0;
    const int batchSize = 1000;
    var batch = new List<EnforcementAction>();

    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        BadDataFound = null
    };

    using var reader = new StreamReader(filePath);
    using var csv = new CsvReader(reader, csvConfig);

    await csv.ReadAsync();
    csv.ReadHeader();

    var headers = csv.HeaderRecord ?? Array.Empty<string>();
    bool hasRemovalDate = headers.Contains("Removal Date");

    while (await csv.ReadAsync())
    {
        try
        {
            // Parse action date
            DateTime actionDate = DateTime.MinValue;
            if (hasRemovalDate)
            {
                var removalDateStr = csv.GetField("Removal Date");
                if (!DateTime.TryParse(removalDateStr, out actionDate))
                    DateTime.TryParse(csv.GetField("Departed Date"), out actionDate);
            }
            else
            {
                DateTime.TryParse(csv.GetField("Departed Date"), out actionDate);
            }

            // Convert to UTC — PostgreSQL requires UTC timestamps
            if (actionDate != DateTime.MinValue)
                actionDate = DateTime.SpecifyKind(actionDate, DateTimeKind.Utc);

            if (actionDate == DateTime.MinValue) { skipped++; continue; }

            var nationality = csv.GetField("Citizenship Country")?.Trim();
            if (string.IsNullOrWhiteSpace(nationality)) { skipped++; continue; }

            var dispositionCode = csv.GetField("Processing Disposition Code")?.Trim();
            var aor = csv.GetField("Area of Responsibility")?.Trim();
            var charge = csv.GetField("Final Charge Section")?.Trim();

            // Criminal if disposition suggests enforcement action
            var criminalCodes = new HashSet<string> { "ER", "REINST", "V", "T", "ADMDPT" };
            bool? isCriminal = !string.IsNullOrEmpty(dispositionCode)
                ? criminalCodes.Contains(dispositionCode.ToUpper())
                : null;

            var action = EnforcementAction.Create(
                type: EnforcementActionType.Removal,
                actionDate: actionDate,
                dataSource: DataSource.ICE_ERO,
                nationality: nationality.ToUpperInvariant(),
                areaOfResponsibility: aor,
                apprehensionMethod: dispositionCode,
                hasCriminalConviction: isCriminal,
                mostSeriousOffense: charge
            );

            batch.Add(action);
            imported++;

            if (batch.Count >= batchSize)
            {
                foreach (var a in batch)
                    await uow.EnforcementActions.AddAsync(a);
                await uow.SaveChangesAsync();
                batch.Clear();

                if (imported % 50000 == 0)
                    logger.LogInformation("  Progress: {Count:N0} records...", imported);
            }
        }
        catch
        {
            skipped++;
        }
    }

    // Save remaining
    if (batch.Count > 0)
    {
        await uow.SaveChangesAsync();
        batch.Clear();
    }

    return (imported, skipped);
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static bool TryParseCount(string? value, out int result)
{
    result = 0;
    if (string.IsNullOrWhiteSpace(value) || value == "D" || value == "-")
        return false;
    return int.TryParse(value.Replace(",", "").Trim(), out result);
}

static string GenerateCode(string countryName)
{
    var clean = countryName.ToUpper()
        .Replace(" ", "")
        .Replace(",", "")
        .Replace("(", "")
        .Replace(")", "")
        .Replace("-", "");
    return clean.Length >= 3 ? clean[..3] : clean.PadRight(3, 'X');
}