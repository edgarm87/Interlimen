using ClosedXML.Excel;
using Meridian.Domain.Entities;
using Meridian.Domain.Interfaces;
using Meridian.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
if (!File.Exists(ohssFile))
{
    logger.LogError("File not found: {File}", ohssFile);
    return;
}

logger.LogInformation("Importing OHSS Table 42 from {File}", ohssFile);

using (var scope = host.Services.CreateScope())
{
    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var result = await ImportOHSSTable42Async(ohssFile, uow, logger);
    logger.LogInformation("Import complete — Imported: {Imported}, Skipped: {Skipped}, Errors: {Errors}",
        result.imported, result.skipped, result.errors);
}

logger.LogInformation("Meridian Importer finished.");

// ── Importer logic ────────────────────────────────────────────────────────────

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

    // Track what we've already queued in this session to avoid duplicates
    var queued = new HashSet<string>();

    // Clear any partial data from previous failed run
    var existing = await uow.RemovalStatistics.GetAllAsync();
    foreach (var e in existing)
        await uow.RemovalStatistics.DeleteAsync(e);
    await uow.SaveChangesAsync();
    logger.LogInformation("Cleared {Count} existing records", existing.Count);

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
                var code = GenerateCode(countryName);
                var stat = RemovalStatistic.Create(
                    year: year,
                    month: 0,
                    nationality: code,
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
                    logger.LogInformation("Progress: {Count} records imported...", imported);
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

static bool TryParseCount(string? value, out int result)
{
    result = 0;
    if (string.IsNullOrWhiteSpace(value) || value == "D" || value == "-")
        return false;
    return int.TryParse(value.Replace(",", "").Trim(), out result);
}

static string GenerateCode(string countryName)
{
    // Use first 3 chars only — display name is the real identifier
    var clean = countryName.ToUpper()
        .Replace(" ", "")
        .Replace(",", "")
        .Replace("(", "")
        .Replace(")", "")
        .Replace("-", "");
    return clean.Length >= 3 ? clean[..3] : clean.PadRight(3, 'X');
}