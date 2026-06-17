using Reporting.Application.Common;

namespace Reporting.Application.Governance;

/// <summary>وحدة الحوكمة: المخاطر والتصعيدات والقرارات.</summary>
public interface IGovernanceService
{
    // Risks
    Task<Result<RiskDto>> CreateRiskAsync(CreateRiskRequest request, CancellationToken ct = default);
    Task<Result<RiskDto>> UpdateRiskAsync(Guid id, UpdateRiskRequest request, CancellationToken ct = default);
    Task<Result<RiskDto>> GetRiskAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RiskDto>>> ListRisksAsync(RiskFilter filter, CancellationToken ct = default);

    // Escalations
    Task<Result<EscalationDto>> CreateEscalationAsync(CreateEscalationRequest request, CancellationToken ct = default);
    Task<Result<EscalationDto>> ResolveEscalationAsync(Guid id, ResolveEscalationRequest request, CancellationToken ct = default);
    Task<Result<EscalationDto>> GetEscalationAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<EscalationDto>>> ListEscalationsAsync(EscalationFilter filter, CancellationToken ct = default);

    // Decisions
    Task<Result<DecisionDto>> CreateDecisionAsync(CreateDecisionRequest request, CancellationToken ct = default);
    Task<Result<DecisionDto>> UpdateDecisionAsync(Guid id, UpdateDecisionRequest request, CancellationToken ct = default);
    Task<Result<DecisionDto>> GetDecisionAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<DecisionDto>>> ListDecisionsAsync(DecisionFilter filter, CancellationToken ct = default);
}
