using Meridian.Domain.Interfaces;
using MediatR;
using Meridian.Domain.Entities;

namespace Meridian.Application.Features.Courts;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CourtBacklogDto(
    string CourtCity,
    string CourtState,
    int TotalPendingCases,
    int ScheduledNextMonth,
    double AverageWaitDays,
    string BacklogSeverity,
    DateTime AsOf
);

// ── Queries ───────────────────────────────────────────────────────────────────

public record GetCourtBacklog(
    string? CourtCity = null,
    string? CourtState = null) : IRequest<List<CourtBacklogDto>>;

public class GetCourtBacklogHandler : IRequestHandler<GetCourtBacklog, List<CourtBacklogDto>>
{
    private readonly ICourtCaseRepository _cases;

    public GetCourtBacklogHandler(ICourtCaseRepository cases) => _cases = cases;

    public async Task<List<CourtBacklogDto>> Handle(
        GetCourtBacklog request,
        CancellationToken cancellationToken)
    {
        var summary = await _cases.GetBacklogSummaryAsync(
            request.CourtCity,
            request.CourtState,
            cancellationToken);

        var severity = summary.TotalPending switch
        {
            < 1000 => "Low",
            < 3000 => "Moderate",
            < 6000 => "High",
            _ => "Critical"
        };

        return new List<CourtBacklogDto>
        {
            new CourtBacklogDto(
                summary.CourtCity,
                summary.CourtState,
                summary.TotalPending,
                summary.ScheduledForNextMonth,
                summary.AverageWaitDays,
                severity,
                summary.AsOf
            )
        };
    }
}

public record GetCasesByCourtAndNationality(
    string CourtCity,
    string? CourtState = null,
    string? Nationality = null,
    string? Outcome = null) : IRequest<List<CourtCase>>;

public class GetCasesByCourtAndNationalityHandler
    : IRequestHandler<GetCasesByCourtAndNationality, List<CourtCase>>
{
    private readonly ICourtCaseRepository _cases;
    public GetCasesByCourtAndNationalityHandler(ICourtCaseRepository cases) => _cases = cases;

    public async Task<List<CourtCase>> Handle(
        GetCasesByCourtAndNationality request, CancellationToken ct)
    {
        CaseOutcome? outcome = null;
        if (!string.IsNullOrWhiteSpace(request.Outcome) &&
            Enum.TryParse<CaseOutcome>(request.Outcome, out var parsed))
            outcome = parsed;

        var cases = await _cases.GetByCourtFilteredAsync(
            request.CourtCity,
            request.CourtState,
            request.Nationality,
            outcome,
            ct);

        return cases.ToList();
    }
}

public record GetPendingByNationality(string Nationality) : IRequest<List<CourtCase>>;

public class GetPendingByNationalityHandler
    : IRequestHandler<GetPendingByNationality, List<CourtCase>>
{
    private readonly ICourtCaseRepository _cases;
    public GetPendingByNationalityHandler(ICourtCaseRepository cases) => _cases = cases;

    public async Task<List<CourtCase>> Handle(
        GetPendingByNationality request, CancellationToken ct)
    {
        var cases = await _cases.GetPendingByNationalityAsync(request.Nationality, ct);
        return cases.Take(200).ToList();
    }
}

public record GetCaseByNumber(string CaseNumber) : IRequest<CourtCase?>;

public class GetCaseByNumberHandler : IRequestHandler<GetCaseByNumber, CourtCase?>
{
    private readonly ICourtCaseRepository _cases;
    public GetCaseByNumberHandler(ICourtCaseRepository cases) => _cases = cases;

    public async Task<CourtCase?> Handle(GetCaseByNumber request, CancellationToken ct)
        => await _cases.GetByCaseNumberAsync(request.CaseNumber.Trim(), ct);
}

public record GetCasesByJudge(
    Guid JudgeId,
    int? FromYear = null,
    int? ToYear = null,
    string? SortBy = null) : IRequest<List<CourtCase>>;

public class GetCasesByJudgeHandler : IRequestHandler<GetCasesByJudge, List<CourtCase>>
{
    private readonly ICourtCaseRepository _cases;
    public GetCasesByJudgeHandler(ICourtCaseRepository cases) => _cases = cases;

    public async Task<List<CourtCase>> Handle(GetCasesByJudge request, CancellationToken ct)
    {
        var cases = await _cases.GetByJudgeAsync(
            request.JudgeId,
            null,
            ct,
            request.FromYear,
            request.ToYear,
            request.SortBy);
        return cases.ToList();
    }
}