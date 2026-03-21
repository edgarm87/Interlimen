using Meridian.Domain.Interfaces;
using MediatR;

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