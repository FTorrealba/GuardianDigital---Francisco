using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GuardianDigital.Application.Features.Alerts;

public static class EvaluateMedicalRisk
{
    public record EvaluateMedicalRiskResponse(
        Guid IncidentId,
        string Status,
        string FinalRiskLevel,
        string DiagnosticSummary,
        string AppliedRuleOrCriteria,
        IReadOnlyList<string> PrioritizationFactors,
        bool HardRuleTriggered,
        DateTime EvaluatedAt
    );

    public static async Task<IResult> HandleAsync(
        Guid id,
        IRiskEvaluationService riskEvaluator,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await riskEvaluator.EvaluateIncidentRiskAsync(id, cancellationToken);

            var response = new EvaluateMedicalRiskResponse(
                IncidentId: id,
                Status: "UnderEvaluation",
                FinalRiskLevel: result.FinalRiskLevel.ToString(),
                DiagnosticSummary: result.DiagnosticSummary,
                AppliedRuleOrCriteria: result.AppliedRuleOrCriteria,
                PrioritizationFactors: result.PrioritizationFactors,
                HardRuleTriggered: result.HardRuleTriggered,
                EvaluatedAt: result.EvaluatedAt
            );

            return Results.Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500, title: "Medical Evaluation Error");
        }
    }
}
