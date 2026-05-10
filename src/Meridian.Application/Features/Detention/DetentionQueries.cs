using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Detention;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FacilityPopulationDto(
    Guid FacilityId,
    string Name,
    string City,
    string State,
    string FacilityType,
    int? RatedCapacity,
    int CurrentDetained,
    decimal? OccupancyPercent,
    DateTime SnapshotDate
);

public record PopulationTrendPointDto(
    DateTime Date,
    int TotalDetained,
    int? Criminal,
    int? NonCriminal
);

public record DetentionSummaryDto(
    int CurrentDetained,
    int DetainedLastMonth,
    string TrendDisplay
);

public record DetentionStaySummaryDto(
    int TotalStays,
    int ActiveStays,
    double AverageLengthOfStayDays,
    string TopReleaseReason
);

public record DetentionStayRowDto(
    Guid Id,
    Guid FacilityId,
    string? FacilityName,
    string? CitizenshipCountry,
    string? Gender,
    int? BirthYear,
    string? BookInCriminality,
    string? CaseStatus,
    string? StayReleaseReason,
    string? FacilityReleaseReason,
    DateTime StayBookInDate,
    DateTime? StayBookOutDate,
    double? LengthOfStayDays
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetActiveFacilitiesWithPopulation() : IRequest<List<FacilityPopulationDto>>;
public record GetFacilityTrend(Guid FacilityId, int Months = 12) : IRequest<List<PopulationTrendPointDto>>;
public record GetSystemPopulationTrend(int Months = 24) : IRequest<List<PopulationTrendPointDto>>;
public record GetDetentionSummary() : IRequest<DetentionSummaryDto>;

public record GetDetentionStaySummary() : IRequest<DetentionStaySummaryDto>;

public record GetDetentionStays(
    int Page = 1,
    int PageSize = 50,
    string? FacilityName = null,
    string? Nationality = null,
    string? ReleaseReason = null,
    bool? ActiveOnly = null,
    int? FromYear = null,
    int? ToYear = null) : IRequest<PagedDetentionStaysDto>;

public record PagedDetentionStaysDto(
    List<DetentionStayRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Handlers ──────────────────────────────────────────────────────────────────

public class GetActiveFacilitiesHandler
    : IRequestHandler<GetActiveFacilitiesWithPopulation, List<FacilityPopulationDto>>
{
    private readonly IDetentionFacilityRepository _facilities;
    public GetActiveFacilitiesHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<FacilityPopulationDto>> Handle(
        GetActiveFacilitiesWithPopulation request, CancellationToken ct)
    {
        var facilities = await _facilities.GetActiveWithLatestPopulationAsync(ct);

        return facilities.Select(f =>
        {
            var latest = f.PopulationSnapshots
                .OrderByDescending(s => s.SnapshotDate)
                .FirstOrDefault();
            var current = latest?.TotalDetained ?? 0;
            decimal? occupancy = f.RatedCapacity.HasValue && f.RatedCapacity > 0
                ? Math.Round((decimal)current / f.RatedCapacity.Value * 100, 1)
                : null;

            return new FacilityPopulationDto(
                f.Id, f.Name, f.City, f.State,
                f.Type.ToString(), f.RatedCapacity,
                current, occupancy,
                latest?.SnapshotDate ?? DateTime.UtcNow);
        }).ToList();
    }
}

public class GetFacilityTrendHandler
    : IRequestHandler<GetFacilityTrend, List<PopulationTrendPointDto>>
{
    private readonly IDetentionFacilityRepository _facilities;
    public GetFacilityTrendHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<PopulationTrendPointDto>> Handle(
        GetFacilityTrend request, CancellationToken ct)
    {
        var snapshots = await _facilities.GetPopulationTrendAsync(
            request.FacilityId, request.Months, ct);

        return snapshots.Select(s => new PopulationTrendPointDto(
            s.SnapshotDate, s.TotalDetained, s.CriminalCount, s.NonCriminalCount
        )).ToList();
    }
}

public class GetSystemPopulationTrendHandler
    : IRequestHandler<GetSystemPopulationTrend, List<PopulationTrendPointDto>>
{
    private readonly IDetentionFacilityRepository _facilities;
    public GetSystemPopulationTrendHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<List<PopulationTrendPointDto>> Handle(
        GetSystemPopulationTrend request, CancellationToken ct)
    {
        var results = await _facilities.GetSystemPopulationTrendAsync(request.Months, ct);
        return results
            .Select(x => new PopulationTrendPointDto(x.Date, x.Total, null, null))
            .ToList();
    }
}

public class GetDetentionSummaryHandler
    : IRequestHandler<GetDetentionSummary, DetentionSummaryDto>
{
    private readonly IDetentionFacilityRepository _facilities;
    public GetDetentionSummaryHandler(IDetentionFacilityRepository facilities)
        => _facilities = facilities;

    public async Task<DetentionSummaryDto> Handle(
        GetDetentionSummary request, CancellationToken ct)
    {
        var results = await _facilities.GetSystemPopulationTrendAsync(14, ct);

        if (!results.Any())
            return new DetentionSummaryDto(0, 0, "");

        var latest = results.Last();
        var current = latest.Total;

        var thirtyDaysAgo = latest.Date.AddDays(-30);
        var lastMonth = results
            .Where(x => x.Date <= thirtyDaysAgo)
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();

        var lastMonthTotal = lastMonth == default ? current : lastMonth.Total;
        var diff = current - lastMonthTotal;
        var trend = diff >= 0 ? $"+{diff:N0}" : $"{diff:N0}";

        return new DetentionSummaryDto(current, lastMonthTotal, trend);
    }
}

public class GetDetentionStaySummaryHandler
    : IRequestHandler<GetDetentionStaySummary, DetentionStaySummaryDto>
{
    private readonly IDetentionStayRepository _repo;
    public GetDetentionStaySummaryHandler(IDetentionStayRepository repo) => _repo = repo;

    public async Task<DetentionStaySummaryDto> Handle(
        GetDetentionStaySummary request, CancellationToken ct)
    {
        var (total, active, avgDays, topReason) = await _repo.GetSummaryAsync(ct);
        return new DetentionStaySummaryDto(total, active, avgDays, topReason);
    }
}

public class GetDetentionStaysHandler
    : IRequestHandler<GetDetentionStays, PagedDetentionStaysDto>
{
    private readonly IDetentionStayRepository _repo;
    public GetDetentionStaysHandler(IDetentionStayRepository repo) => _repo = repo;

    public async Task<PagedDetentionStaysDto> Handle(
        GetDetentionStays request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPagedAsync(
            request.Page, request.PageSize, request.FacilityName,
            request.Nationality, request.ReleaseReason,
            request.ActiveOnly, request.FromYear, request.ToYear, ct);

        var rows = items.Select(d => new DetentionStayRowDto(
            d.Id,
            d.FacilityId,
            d.FacilityName,
            d.Nationality,
            d.Gender,
            d.BirthYear,
            d.Criminality,
            d.CaseStatus,
            d.StayReleaseReason,
            d.FacilityReleaseReason,
            d.BookInDate,
            d.BookOutDate,
            d.BookOutDate.HasValue
                ? (d.BookOutDate.Value - d.BookInDate).TotalDays
                : null
        )).ToList();

        return new PagedDetentionStaysDto(rows, total, request.Page, request.PageSize);
    }
}
