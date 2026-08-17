namespace GuardianDigital.Application.Common.Models;

public record AgentLogDto(
    Guid Id,
    DateTime Timestamp,
    string AgentName,
    string CycleStage,
    string Message,
    string? Details = null,
    Guid? IncidentId = null
);
