using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GuardianDigital.Application.Features.Alerts;

public static class AlertsFeature
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/incidents", (IGuardianDbContext db, Guid? userId) =>
            GetIncidents.HandleAsync(db, userId))
            .WithName("GetIncidents")
            .WithTags("Incidents");

        app.MapGet("/api/incidents/agent-logs", (IAgentLogService logService, int count = 50) =>
            GetAgentLogs.Handle(logService, count))
            .WithName("GetAgentLogs")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/{id:guid}/evaluate", (Guid id, EvaluateIncident.EvaluateIncidentRequest request, IGuardianDbContext db, IAgentLogService agentLogger) =>
            EvaluateIncident.HandleAsync(id, request, db, agentLogger))
            .WithName("EvaluateIncident")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/report-symptom", (ReportSymptom.ReportSymptomRequest request, IGuardianDbContext db, ILanguageModelService llmService, IAgentLogService agentLogger, CancellationToken ct) =>
            ReportSymptom.HandleAsync(request, db, llmService, agentLogger, ct))
            .WithName("ReportSymptom")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/{id:guid}/evaluate-medical", (Guid id, IRiskEvaluationService riskEvaluator, CancellationToken ct) =>
            EvaluateMedicalRisk.HandleAsync(id, riskEvaluator, ct))
            .WithName("EvaluateMedicalRisk")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/{id:guid}/dispatch-actions", (Guid id, IEmergencyManagementService emergencyService, CancellationToken ct) =>
            DispatchEmergencyAction.HandleAsync(id, emergencyService, ct))
            .WithName("DispatchEmergencyAction")
            .WithTags("Incidents");

        app.MapGet("/api/incidents/{id:guid}/rescue-sheet", (Guid id, IEmergencyManagementService emergencyService, CancellationToken ct) =>
            GetRescueSheet.HandleAsync(id, emergencyService, ct))
            .WithName("GetRescueSheet")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/{id:guid}/request-appointment", (Guid id, RequestAppointment.RequestAppointmentRequest request, IEmergencyManagementService emergencyService, CancellationToken ct) =>
            RequestAppointment.HandleAsync(id, request, emergencyService, ct))
            .WithName("RequestAppointment")
            .WithTags("Incidents");

        app.MapPost("/api/incidents/{id:guid}/false-alarm", (Guid id, MarkFalseAlarm.MarkFalseAlarmRequest request, IGuardianDbContext db, IAgentLogService agentLogger, CancellationToken ct) =>
            MarkFalseAlarm.HandleAsync(id, request, db, agentLogger, ct))
            .WithName("MarkFalseAlarm")
            .WithTags("Incidents");
    }
}
