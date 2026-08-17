using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Alerts;

public static class ReportSymptom
{
    public record ReportSymptomRequest(
        string Message,
        string? Origin = "Voice", // "Voice" | "Text"
        Guid? UserId = null
    );

    public record ReportSymptomResponse(
        Guid IncidentId,
        string Origin,
        IReadOnlyList<string> DetectedSymptoms,
        IReadOnlyList<string> SuggestedQuestions,
        string SuggestedUrgencyLevel, // "mild" | "urgent" | "possible_emergency"
        string ConversationalResponse,
        DateTime Timestamp
    );

    public static async Task<IResult> HandleAsync(
        ReportSymptomRequest request,
        IGuardianDbContext db,
        ILanguageModelService llmService,
        IAgentLogService agentLogger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "Symptom message cannot be empty." });
        }

        // Parse Origin
        var origin = IncidentOrigin.Voice;
        if (!string.IsNullOrWhiteSpace(request.Origin) &&
            Enum.TryParse<IncidentOrigin>(request.Origin, true, out var parsedOrigin))
        {
            origin = parsedOrigin;
        }

        // Step 1: Agent Observation Log
        agentLogger.Log(
            agentName: "InteractionAgent",
            cycleStage: "Observation",
            message: $"Received user symptom report via {origin}.",
            details: $"Message: \"{request.Message}\""
        );

        // Step 2: LLM Interpretation & Clinical Follow-up Generation
        var llmResult = await llmService.InterpretSymptomAsync(request.Message, cancellationToken);

        agentLogger.Log(
            agentName: "InteractionAgent",
            cycleStage: "Analysis",
            message: $"LLM interpretation completed. Urgency: '{llmResult.SuggestedUrgencyLevel}'.",
            details: $"Detected Symptoms: [{string.Join(", ", llmResult.DetectedSymptoms)}] | Suggested Questions: [{string.Join(" | ", llmResult.SuggestedQuestions)}]"
        );

        // Map Urgency Level to Domain RiskLevel
        var riskLevel = llmResult.SuggestedUrgencyLevel.ToLowerInvariant() switch
        {
            "possible_emergency" => RiskLevel.PossibleEmergency,
            "urgent" => RiskLevel.Urgent,
            _ => RiskLevel.Mild
        };

        // Determine Associated User
        var targetUserId = request.UserId ?? Guid.Empty;
        if (targetUserId == Guid.Empty)
        {
            var firstUser = await db.Users.FirstOrDefaultAsync(cancellationToken);
            if (firstUser != null)
            {
                targetUserId = firstUser.Id;
            }
            else
            {
                // Fallback default user if DB not yet seeded
                targetUserId = Guid.NewGuid();
            }
        }

        // Step 3: Decision & Incident Creation in SQLite
        var incident = new Incident
        {
            UserId = targetUserId,
            Timestamp = DateTime.UtcNow,
            Origin = origin,
            OriginalDescription = $"Reported: \"{request.Message}\" | Symptoms: {string.Join(", ", llmResult.DetectedSymptoms)}",
            RiskLevel = riskLevel,
            Status = IncidentStatus.Detected
        };

        // Attach suggested questions as pending user responses
        foreach (var q in llmResult.SuggestedQuestions)
        {
            incident.UserResponses.Add(new UserResponse
            {
                Question = q,
                Answer = "(Awaiting response)",
                Timestamp = DateTime.UtcNow
            });
        }

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);

        agentLogger.Log(
            agentName: "InteractionAgent",
            cycleStage: "Decision",
            message: $"Incident registered with status 'Detected' and risk '{riskLevel}'.",
            details: $"Incident ID: {incident.Id} | Origin: {origin}",
            incidentId: incident.Id
        );

        var response = new ReportSymptomResponse(
            IncidentId: incident.Id,
            Origin: origin.ToString(),
            DetectedSymptoms: llmResult.DetectedSymptoms,
            SuggestedQuestions: llmResult.SuggestedQuestions,
            SuggestedUrgencyLevel: llmResult.SuggestedUrgencyLevel,
            ConversationalResponse: llmResult.ConversationalResponse,
            Timestamp: incident.Timestamp
        );

        return Results.Ok(response);
    }
}
