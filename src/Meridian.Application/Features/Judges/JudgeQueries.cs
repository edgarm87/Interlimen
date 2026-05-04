using Meridian.Domain.Interfaces;
using MediatR;

namespace Meridian.Application.Features.Judges;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record JudgeSummaryDto(
    Guid Id,
    string FullName,
    string CourtCity,
    string CourtState,
    int TotalCasesDecided,
    decimal GrantRatePercent,
    decimal DenialRatePercent,
    decimal RemovalOrderRatePercent,
    double? AverageDaysToDecision,
    DateTime? StatsLastRefreshed
);

// ── Queries ───────────────────────────────────────────────────────────────────
public record GetJudgeById(Guid Id) : IRequest<JudgeSummaryDto?>;

public class GetJudgeByIdHandler : IRequestHandler<GetJudgeById, JudgeSummaryDto?>
{
    private readonly IJudgeRepository _judges;
    public GetJudgeByIdHandler(IJudgeRepository judges) => _judges = judges;

    public async Task<JudgeSummaryDto?> Handle(GetJudgeById request, CancellationToken ct)
    {
        var judge = await _judges.GetByIdAsync(request.Id, ct);
        if (judge == null) return null;
        return new JudgeSummaryDto(
            judge.Id,
            judge.FullName,
            judge.CourtCity,
            judge.CourtState,
            judge.TotalCasesDecided,
            judge.GrantRatePercent,
            judge.DenialRatePercent,
            judge.RemovalOrderRatePercent,
            judge.AverageDaysToDecision,
            judge.StatsLastRefreshed);
    }
}

public record GetAllJudgesWithStats() : IRequest<List<JudgeSummaryDto>>;

public class GetAllJudgesWithStatsHandler : IRequestHandler<GetAllJudgesWithStats, List<JudgeSummaryDto>>
{
    private readonly IJudgeRepository _judges;

    public GetAllJudgesWithStatsHandler(IJudgeRepository judges) => _judges = judges;

    public async Task<List<JudgeSummaryDto>> Handle(
        GetAllJudgesWithStats request,
        CancellationToken cancellationToken)
    {
        var judges = await _judges.GetWithStatsAsync(cancellationToken);

        return judges.Select(j => new JudgeSummaryDto(
            j.Id,
            j.FullName,
            j.CourtCity,
            j.CourtState,
            j.TotalCasesDecided,
            j.GrantRatePercent,
            j.DenialRatePercent,
            j.RemovalOrderRatePercent,
            j.AverageDaysToDecision,
            j.StatsLastRefreshed
        )).ToList();
    }
}

public record GetJudgesByCourt(string CourtCity, string CourtState) : IRequest<List<JudgeSummaryDto>>;

public class GetJudgesByCourtHandler : IRequestHandler<GetJudgesByCourt, List<JudgeSummaryDto>>
{
    private readonly IJudgeRepository _judges;

    public GetJudgesByCourtHandler(IJudgeRepository judges) => _judges = judges;

    public async Task<List<JudgeSummaryDto>> Handle(
        GetJudgesByCourt request,
        CancellationToken cancellationToken)
    {
        var judges = await _judges.GetByCourtAsync(
            request.CourtCity,
            request.CourtState,
            cancellationToken);

        return judges.Select(j => new JudgeSummaryDto(
            j.Id,
            j.FullName,
            j.CourtCity,
            j.CourtState,
            j.TotalCasesDecided,
            j.GrantRatePercent,
            j.DenialRatePercent,
            j.RemovalOrderRatePercent,
            j.AverageDaysToDecision,
            j.StatsLastRefreshed
        )).ToList();
    }
}