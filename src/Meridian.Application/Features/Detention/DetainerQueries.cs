using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Detention;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record DetainerSummaryDto(
    int TotalDetainers,
    int ActiveDetainers,
    int ResultedInRemoval,
    string TopState
);

public record DetainerRowDto(
    Guid Id,
    DateTime PrepareDate,
    string? FacilityState,
    string? FacilityAor,
    string? FacilityName,
    string? CitizenshipCountry,
    string? Criminality,
    string? CaseStatus,
    DateTime? DepartedDate,
    string? DepartureCountry,
    bool IsActive
);

public record PagedDetainersDto(
    List<DetainerRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetDetainerSummary() : IRequest<DetainerSummaryDto>;

public record GetDetainers(
    int Page = 1,
    int PageSize = 50,
    string? State = null,
    string? CitizenshipCountry = null,
    string? Criminality = null,
    string? FacilityName = null,
    bool? ActiveOnly = null,
    string SortBy = "PrepareDate",
    bool SortDescending = true) : IRequest<PagedDetainersDto>;

// ── Handlers ──────────────────────────────────────────────────────────────────

public class GetDetainerSummaryHandler
    : IRequestHandler<GetDetainerSummary, DetainerSummaryDto>
{
    private readonly IIceDetainerRepository _repo;
    public GetDetainerSummaryHandler(IIceDetainerRepository repo) => _repo = repo;

    public async Task<DetainerSummaryDto> Handle(
        GetDetainerSummary request, CancellationToken ct)
    {
        var (total, active, removed, topState) = await _repo.GetSummaryAsync(ct);
        return new DetainerSummaryDto(total, active, removed, topState);
    }
}

public class GetDetainersHandler
    : IRequestHandler<GetDetainers, PagedDetainersDto>
{
    private readonly IIceDetainerRepository _repo;
    public GetDetainersHandler(IIceDetainerRepository repo) => _repo = repo;

    public async Task<PagedDetainersDto> Handle(
        GetDetainers request, CancellationToken ct)
    {
        var (items, total) = await _repo.GetPagedAsync(
            request.Page, request.PageSize, request.State,
            request.CitizenshipCountry, request.Criminality,
            request.FacilityName, request.ActiveOnly, ct, request.SortBy, request.SortDescending);

        var rows = items.Select(d => new DetainerRowDto(
            d.Id, d.PrepareDate, d.State, d.Aor, d.FacilityName,
            d.Country, d.Criminality, d.CaseStatus,
            d.DepartedDate, d.DepartureCountry,
            d.DepartedDate == null
        )).ToList();

        return new PagedDetainersDto(rows, total, request.Page, request.PageSize);
    }
}