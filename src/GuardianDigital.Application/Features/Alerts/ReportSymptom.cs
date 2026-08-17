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
        string? Origin = null,
        Guid? UserId = null
    );

    public record ReportSymptomResponse(
        Guid IncidentId,
        string Origin,
        IReadOnlyList<string> DetectedSymptoms,
        IReadOnlyList<string> SuggestedQuestions,
        string SuggestedUrgencyLevel,
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
            return Results.BadRequest(new { error = "El mensaje o reporte de síntomas no puede estar vacío." });
        }

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
            message: $"Reporte de síntomas recibido vía {origin}.",
            details: $"Mensaje: \"{request.Message}\""
        );

        // Step 2: LLM Interpretation & Clinical Follow-up Generation
        var llmResult = await llmService.InterpretSymptomAsync(request.Message, cancellationToken);

        agentLogger.Log(
            agentName: "InteractionAgent",
            cycleStage: "Analysis",
            message: $"Interpretación del modelo completada. Urgencia clínica: '{llmResult.SuggestedUrgencyLevel}'.",
            details: $"Síntomas detectados: [{string.Join(", ", llmResult.DetectedSymptoms)}] | Preguntas sugeridas: [{string.Join(" | ", llmResult.SuggestedQuestions)}]"
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
            OriginalDescription = $"Reporte: \"{request.Message}\" | Síntomas detectados: {string.Join(", ", llmResult.DetectedSymptoms)}",
            RiskLevel = riskLevel,
            Status = IncidentStatus.Detected
        };

        // Attach suggested questions as pending user responses
        foreach (var q in llmResult.SuggestedQuestions)
        {
            incident.UserResponses.Add(new UserResponse
            {
                Question = q,
                Answer = "(Esperando respuesta del paciente)",
                Timestamp = DateTime.UtcNow
            });
        }

        db.Incidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);

        agentLogger.Log(
            agentName: "InteractionAgent",
            cycleStage: "Decision",
            message: $"Incidente registrado con estado 'Detectado' y riesgo '{riskLevel}'.",
            details: $"ID de Incidente: {incident.Id} | Origen: {origin}",
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
