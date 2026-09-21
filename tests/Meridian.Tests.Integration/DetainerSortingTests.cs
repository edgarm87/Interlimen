using Meridian.Domain.Entities;
using Meridian.Infrastructure.Persistence;
using Meridian.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Tests.Integration;

public class DetainerSortingTests
{
    [Theory]
    [InlineData("PrepareDate")]
    [InlineData("State")]
    [InlineData("Facility")]
    [InlineData("Nationality")]
    [InlineData("Criminality")]
    [InlineData("Status")]
    [InlineData("Departed")]
    [InlineData("Destination")]
    public async Task SortingPrecedesPaginationAndReversesDirection(string column)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var first = IceDetainer.Create(new DateTime(2025, 1, 1), default,
            facilityState: "ALABAMA", detentionFacilityName: "Alpha", departureCountry: "Canada",
            citizenshipCountry: "Canada", detainerCriminality: "1 Convicted Criminal");
        var second = IceDetainer.Create(new DateTime(2025, 2, 1), default,
            facilityState: "TEXAS", detentionFacilityName: "Zulu", departureCountry: "Mexico",
            departedDate: new DateTime(2025, 3, 1), citizenshipCountry: "Mexico",
            detainerCriminality: "2 Pending Criminal Charges");
        db.IceDetainers.AddRange(second, first);
        await db.SaveChangesAsync();
        var repo = new IceDetainerRepository(db);
        var ascending = await repo.GetPagedAsync(1, 1, null, null, null, null, null,
            sortBy: column, sortDescending: false);
        var nextPage = await repo.GetPagedAsync(2, 1, null, null, null, null, null,
            sortBy: column, sortDescending: false);
        var descending = await repo.GetPagedAsync(1, 1, null, null, null, null, null,
            sortBy: column, sortDescending: true);
        Assert.Equal(2, ascending.Total);
        Assert.NotEqual(ascending.Items.Single().Id, nextPage.Items.Single().Id);
        Assert.Equal(nextPage.Items.Single().Id, descending.Items.Single().Id);
        // Nullable date ordering varies by database provider.
        if (column != "Departed") Assert.Equal(first.Id, ascending.Items.Single().Id);
    }
}
