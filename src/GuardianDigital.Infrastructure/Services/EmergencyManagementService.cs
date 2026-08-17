using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Emergency Management Agent responsible for multi-tiered action dispatch based on RiskLevel,
/// family notification broadcasting, emergency services dispatch countdown,
/// and first-responder Rescue Medical Sheet access.
/// </summary>
public class EmergencyManagementService : IEmergencyManagementService
{
    private readonly IGuardianDbContext _db;
    private readonly IAgentLogService _agentLogger;
    private readonly ILogger<EmergencyManagementService> _logger;

    public EmergencyManagementService(
        IGuardianDbContext db,
        IAgentLogService agentLogger,
        ILogger<EmergencyManagementService> logger)
    {
        _db = db;
        _agentLogger = agentLogger;
        _logger = logger;
    }

    public async Task<EmergencyDispatchResult> DispatchActionsAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.ActionsExecuted)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident == null)
        {
            throw new KeyNotFoundException($"No se encontró el incidente con ID '{incidentId}'.");
        }

        var contacts = await _db.EmergencyContacts
            .Where(c => c.UserId == incident.UserId)
            .ToListAsync(cancellationToken);

        var dispatchedActions = new List<ActionExecuted>();
        bool emergencyProtocolActivated = false;
        int? countdownSeconds = null;

        // Step 1: Agent Observation Log
        _agentLogger.Log(
            agentName: "EmergencyManagementAgent",
            cycleStage: "Observation",
            message: $"Evaluando protocolo de despacho de acciones para Incidente #{incident.Id} (Nivel de Riesgo: {incident.RiskLevel}).",
            details: $"Origen: {incident.Origin} | Descripción: '{incident.OriginalDescription}' | Contactos registrados: {contacts.Count}",
            incidentId: incident.Id
        );

        // Step 2: Action Dispatch by Risk Level
        switch (incident.RiskLevel)
        {
            case RiskLevel.Mild:
                // Action: General Recommendation
                var mildAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.GeneralRecommendation,
                    Timestamp = DateTime.UtcNow,
                    Result = "Recomendación de cuidado general emitida: Descansar en un entorno tranquilo, mantener buena hidratación y observar signos vitales. Opción de solicitud de turno médico no urgente habilitada."
                };
                dispatchedActions.Add(mildAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: "Protocolo de riesgo leve: Recomendación general de salud generada. Turno facultativo disponible.",
                    details: mildAction.Result,
                    incidentId: incident.Id
                );
                break;

            case RiskLevel.Urgent:
                // Action 1: Simulated Notification to Emergency Contacts
                var contactSummary = contacts.Count > 0
                    ? string.Join(", ", contacts.Select(c => $"{c.ContactName} [{c.Relationship}]: {c.Phone} ({c.PreferredMethod})"))
                    : "Contactos familiares principales (Lista predeterminada de emergencias)";

                var urgentNotifyAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.NotifyFamily,
                    Timestamp = DateTime.UtcNow,
                    Result = $"Notificación de alerta enviada a contactos de emergencia: {contactSummary}. Mensaje: 'Alerta médica urgente para el paciente. Se aconseja acompañamiento y evaluación clínica.'"
                };
                dispatchedActions.Add(urgentNotifyAction);

                // Action 2: Travel / Urgent Clinic Guidance
                var urgentTravelAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.GeneralRecommendation,
                    Timestamp = DateTime.UtcNow,
                    Result = "Indicación de traslado emitida: Dirigirse a una guardia o centro médico de urgencias con una persona acompañante. Evitar conducir si persisten mareos o debilidad."
                };
                dispatchedActions.Add(urgentTravelAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: $"Protocolo de riesgo urgente: Notificaciones simuladas enviadas a {contacts.Count} contacto(s) e indicación de traslado clínico emitida.",
                    details: urgentNotifyAction.Result,
                    incidentId: incident.Id
                );
                break;

            case RiskLevel.PossibleEmergency:
            default:
                emergencyProtocolActivated = true;
                countdownSeconds = 30;

                // Action 1: High-Priority Emergency Broadcast to ALL Contacts
                var allContactsStr = contacts.Count > 0
                    ? string.Join(", ", contacts.Select(c => $"{c.ContactName} ({c.Phone})"))
                    : "Todos los contactos de emergencia registrados";

                var emergencyNotifyAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.NotifyFamily,
                    Timestamp = DateTime.UtcNow,
                    Result = $"PROTOCOLO DE EMERGENCIA CRÍTICA ACTIVADO: Difusión de máxima prioridad enviada a TODOS los contactos ({allContactsStr}). Telemetría de rescate habilitada para primeros intervinientes."
                };
                dispatchedActions.Add(emergencyNotifyAction);

                // Action 2: Contact Emergency Services (911 / 107 / EMS) with 30s Countdown
                var emergencyServicesAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.ContactEmergencyServices,
                    Timestamp = DateTime.UtcNow,
                    Result = "DESPACHO DE EMERGENCIAS (107 / SAME / 911): Cuenta regresiva de 30 segundos iniciada para el despacho automático de ambulancia. Ficha Médica de Rescate expuesta para paramédicos."
                };
                dispatchedActions.Add(emergencyServicesAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: "PROTOCOLO DE POSIBLE EMERGENCIA ACTIVADO: Contactos notificados e inicio de cuenta regresiva para ambulancia.",
                    details: emergencyServicesAction.Result,
                    incidentId: incident.Id
                );
                break;
        }

        // Step 3: Persistence & Transition to ActionTaken
        foreach (var action in dispatchedActions)
        {
            _db.ActionsExecuted.Add(action);
        }

        incident.Status = IncidentStatus.ActionTaken;
        await _db.SaveChangesAsync(cancellationToken);

        // Step 4: Decision Log
        _agentLogger.Log(
            agentName: "EmergencyManagementAgent",
            cycleStage: "Decision",
            message: $"Incidente #{incident.Id} transicionado a 'ActionTaken' (Acción Ejecutada). {dispatchedActions.Count} acciones automáticas ejecutadas.",
            details: $"Protocolo de Emergencia: {emergencyProtocolActivated} | Cuenta Regresiva: {countdownSeconds?.ToString() ?? "N/A"}s",
            incidentId: incident.Id
        );

        return new EmergencyDispatchResult(
            IncidentId: incident.Id,
            RiskLevel: incident.RiskLevel,
            Status: incident.Status.ToString(),
            ActionsDispatched: dispatchedActions.Select(a => new DispatchedActionSummary(a.Id, a.ActionType.ToString(), a.Result, a.Timestamp)).ToList(),
            EmergencyProtocolActivated: emergencyProtocolActivated,
            DispatchCountdownSeconds: countdownSeconds,
            DispatchedAt: DateTime.UtcNow
        );
    }

    public async Task<RescueSheetDto> GetRescueSheetAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident == null)
        {
            throw new KeyNotFoundException($"No se encontró el incidente con ID '{incidentId}'.");
        }

        var user = await _db.Users
            .Include(u => u.MedicalProfile)
            .Include(u => u.EmergencyContacts)
            .FirstOrDefaultAsync(u => u.Id == incident.UserId, cancellationToken);

        var medProfile = user?.MedicalProfile;
        var contacts = user?.EmergencyContacts ?? new List<EmergencyContact>();

        int age = 0;
        if (user != null && user.DateOfBirth != default)
        {
            age = DateTime.UtcNow.Year - user.DateOfBirth.Year;
            if (user.DateOfBirth.Date > DateTime.UtcNow.AddYears(-age)) age--;
        }

        return new RescueSheetDto(
            IncidentId: incident.Id,
            IncidentOrigin: incident.Origin.ToString(),
            IncidentRiskLevel: incident.RiskLevel.ToString(),
            IncidentDescription: incident.OriginalDescription,
            IncidentTimestamp: incident.Timestamp,
            PatientId: user?.Id ?? Guid.Empty,
            PatientFullName: user?.FullName ?? "Paciente Desconocido",
            NationalId: user?.NationalId ?? "S/D",
            Age: age,
            Gender: user?.Gender ?? "No especificado",
            PrimaryPhone: user?.PrimaryPhone ?? "S/D",
            Address: user?.Address ?? "S/D",
            HealthInsurance: user?.HealthInsurance ?? "Particular / Sin especificar",
            BloodType: user?.BloodType?.Value ?? "Desconocido",
            KnownAllergies: medProfile?.KnownAllergies ?? new List<string>(),
            PreexistingConditions: medProfile?.PreexistingConditions ?? new List<string>(),
            CurrentMedication: medProfile?.CurrentMedication ?? new List<string>(),
            MedicalHistory: medProfile?.MedicalHistory ?? "Sin antecedentes relevantes registrados",
            EmergencyContacts: contacts.Select(c => new RescueContactDto(c.ContactName, c.Relationship, c.Phone, c.PreferredMethod.ToString())).ToList(),
            GeneratedAt: DateTime.UtcNow
        );
    }

    public async Task<DispatchedActionSummary> RequestMedicalAppointmentAsync(Guid incidentId, string? notes = null, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.ActionsExecuted)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident == null)
        {
            throw new KeyNotFoundException($"No se encontró el incidente con ID '{incidentId}'.");
        }

        var appointmentAction = new ActionExecuted
        {
            IncidentId = incident.Id,
            ActionType = ActionType.RequestMedicalAppointment,
            Timestamp = DateTime.UtcNow,
            Result = $"Turno médico de chequeo solicitado a la red ambulatoria. Notas de seguimiento: \"{notes ?? "El paciente solicitó revisión tras reporte de molestia leve."}\""
        };

        _db.ActionsExecuted.Add(appointmentAction);
        await _db.SaveChangesAsync(cancellationToken);

        _agentLogger.Log(
            agentName: "EmergencyManagementAgent",
            cycleStage: "Decision",
            message: $"Turno médico programado para Incidente #{incident.Id}.",
            details: appointmentAction.Result,
            incidentId: incident.Id
        );

        return new DispatchedActionSummary(
            appointmentAction.Id,
            appointmentAction.ActionType.ToString(),
            appointmentAction.Result,
            appointmentAction.Timestamp
        );
    }
}
