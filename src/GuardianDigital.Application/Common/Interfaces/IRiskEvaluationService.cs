using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Application.Common.Interfaces;

public record RiskEvaluationResult(
    RiskLevel FinalRiskLevel,
    string DiagnosticSummary,
    string AppliedRuleOrCriteria,
    IReadOnlyList<string> PrioritizationFactors,
    bool HardRuleTriggered,
    DateTime EvaluatedAt
);

public interface IRiskEvaluationService
{
    /// <summary>
    /// Evaluates an existing Incident in the database, combines clinical hard rules,
    /// LLM suggestions, and Section 7 prioritization criteria, updates the incident RiskLevel,
    /// transitions status to UnderEvaluation, and returns the result.
    /// </summary>
    Task<RiskEvaluationResult> EvaluateIncidentRiskAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pure functional evaluation combining an incident, medical profile, user demographics,
    /// recent telemetry readings, and recent incident history.
    /// </summary>
    RiskEvaluationResult EvaluateRisk(
        Incident incident,
        MedicalProfile? medicalProfile,
        User? user,
        IEnumerable<SensorReading>? recentReadings = null,
        IEnumerable<Incident>? recentIncidents = null);
}
