using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Alerts;

public static class EvaluateIncident
{
    public record EvaluateIncidentRequest(
        string NewStatus,
        string? Note
    );

    public static async Task<IResult> HandleAsync(Guid id, EvaluateIncidentRequest request, IGuardianDbContext db, IAgentLogService agentLogger)
    {
        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == id);
        if (incident == null)
        {
            return Results.NotFound(new { error = $"Incident with ID '{id}' was not found." });
        }

        if (!Enum.TryParse<IncidentStatus>(request.NewStatus, true, out var parsedStatus))
        {
            return Results.BadRequest(new { error = $"Invalid incident status '{request.NewStatus}'." });
        }

        var oldStatus = incident.Status;
        incident.Status = parsedStatus;
        await db.SaveChangesAsync();

        agentLogger.Log(
            agentName: "EventAnalysis",
            cycleStage: "StatusChange",
            message: $"Incident status updated from {oldStatus} to {parsedStatus}.",
            details: request.Note ?? "Manual or workflow evaluation trigger",
            incidentId: incident.Id
        );

        return Results.Ok(new { message = $"Incident status updated to {parsedStatus}.", incidentId = incident.Id, status = parsedStatus.ToString() });
    }
}
