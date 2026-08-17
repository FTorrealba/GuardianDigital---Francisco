using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Alerts;

public static class MarkFalseAlarm
{
    public record MarkFalseAlarmRequest(string? Reason = "User or family marked as false alarm");

    public static async Task<IResult> HandleAsync(
        Guid id,
        MarkFalseAlarmRequest request,
        IGuardianDbContext db,
        IAgentLogService agentLogger,
        CancellationToken cancellationToken = default)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (incident == null)
        {
            return Results.NotFound(new { error = $"Incident with ID '{id}' was not found." });
        }

        var oldStatus = incident.Status;
        incident.Status = IncidentStatus.FalseAlarm;

        var reasonText = string.IsNullOrWhiteSpace(request.Reason)
            ? "User/family marked as false alarm."
            : request.Reason.Trim();

        incident.OriginalDescription += $" [False Alarm: {reasonText}]";
        await db.SaveChangesAsync(cancellationToken);

        // Emit Learning Agent Audit Log
        agentLogger.Log(
            agentName: "LearningAgent",
            cycleStage: "Decision",
            message: $"Incident #{incident.Id} updated to 'FalseAlarm' (was '{oldStatus}'). Feedback incorporated into persistent memory.",
            details: $"Feedback: '{reasonText}' | Origin: {incident.Origin}",
            incidentId: incident.Id
        );

        return Results.Ok(new
        {
            message = "Incident successfully marked as False Alarm.",
            incidentId = incident.Id,
            status = "FalseAlarm",
            feedback = reasonText,
            timestamp = DateTime.UtcNow
        });
    }
}
