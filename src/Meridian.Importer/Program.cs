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

// Run migrations
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    logger.LogInformation("Database migrations applied");
}

// Determine which command to run
var command = args.FirstOrDefault() ?? "removals";

switch (command)
{
    case "removals":
        await ImportICERemovalsAsync(csvPath, host.Services, logger);
        break;
    case "ohss":
        logger.LogInformation("OHSS import — use the original ohss command");
        break;
    default:
        logger.LogError("Unknown command: {Command}. Use 'removals' or 'ohss'", command);
        break;
}

logger.LogInformation("Meridian Importer finished.");

// ── ICE Removals Importer ─────────────────────────────────────────────────────

static async Task ImportICERemovalsAsync(
    string csvPath,
    IServiceProvider services,
    ILogger logger)
{
    var files = Directory.GetFiles(csvPath, "ice_removals_*.csv")
        .OrderBy(f => f)
        .ToList();

    if (!files.Any())
    {
        logger.LogError("No ICE removal CSV files found in {Path}", csvPath);
        return;
    }

    logger.LogInformation("Found {Count} CSV files to import", files.Count);

    int totalImported = 0;
    int totalSkipped = 0;
    int totalErrors = 0;

    foreach (var file in files)
    {
        var fileName = Path.GetFileName(file);
        logger.LogInformation("Processing {File}...", fileName);

        using var scope = services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var (imported, skipped, errors) = await ImportSingleFileAsync(file, uow, logger);
        totalImported += imported;
        totalSkipped += skipped;
        totalErrors += errors;

        logger.LogInformation(
            "{File} done — Imported: {Imported}, Skipped: {Skipped}, Errors: {Errors}",
            fileName, imported, skipped, errors);
    }

    logger.LogInformation(
        "All files complete — Total Imported: {Total}, Skipped: {Skipped}, Errors: {Errors}",
        totalImported, totalSkipped, totalErrors);
}

static async Task<(int imported, int skipped, int errors)> ImportSingleFileAsync(
    string filePath,
    IUnitOfWork uow,
    ILogger logger)
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
            // Handle two different column formats
            // FY2014+ has "Removal Date" and "Arrest Date"
            // FY2012-2013 has "Departed Date" and no Arrest Date
            var removalDateStr = csv.GetField("Removal Date") 
                                 ?? csv.GetField("Departed Date");
            var arrestDateStr = csv.GetField("Arrest Date") 
                                ?? csv.GetField("Case Close Date");
            var country = csv.GetField("Citizenship Country")?.Trim();
            var aor = csv.GetField("Area of Responsibility")?.Trim();
            var threatLevel = csv.GetField("Removal Threat Level")?.Trim();
            var appMethod = csv.GetField("Apprehension Method Code")?.Trim();
            var chargeSection = csv.GetField("Final Charge Section")?.Trim();

            if (string.IsNullOrWhiteSpace(country)) { skipped++; continue; }
            if (!DateTime.TryParse(removalDateStr, out var removalDate))
            {
                skipped++;
                continue;
            }
            removalDate = DateTime.SpecifyKind(removalDate, DateTimeKind.Utc);

            // Derive criminal status from INA charge section
            // 237(a)(2) = criminal grounds of deportability
            // 212(a)(2) = criminal grounds of inadmissibility
            var hasCriminal = !string.IsNullOrWhiteSpace(chargeSection) &&
                              !chargeSection.StartsWith("(b)") &&
                              (chargeSection.StartsWith("237a2") ||
                               chargeSection.StartsWith("212a2") ||
                               chargeSection.StartsWith("237a2A") ||
                               chargeSection.StartsWith("237a2B"));

            var action = EnforcementAction.Create(
                type: EnforcementActionType.Removal,
                actionDate: removalDate,
                dataSource: DataSource.ICE_ERO,
                nationality: country,
                areaOfResponsibility: aor,
                apprehensionMethod: appMethod,
                hasCriminalConviction: hasCriminal,
                mostSeriousOffense: string.IsNullOrWhiteSpace(chargeSection) ||
                                    chargeSection.StartsWith("(b)") ? null : chargeSection
            );

            batch.Add(action);

            if (batch.Count >= batchSize)
            {
                foreach (var item in batch)
                    await uow.EnforcementActions.AddAsync(item);
                await uow.SaveChangesAsync();
                imported += batch.Count;
                batch.Clear();

                if (imported % 10000 == 0)
                    logger.LogInformation("  Progress: {Count:N0} records...", imported);
            }
        }
        catch (Exception ex)
        {
            errors++;
            if (errors <= 3)
                logger.LogWarning("Row error: {Message}", ex.Message);
        }
    }

    // Final batch
    if (batch.Any())
    {
        foreach (var item in batch)
            await uow.EnforcementActions.AddAsync(item);
        await uow.SaveChangesAsync();
        imported += batch.Count;
    }

    return (imported, skipped, errors);
}