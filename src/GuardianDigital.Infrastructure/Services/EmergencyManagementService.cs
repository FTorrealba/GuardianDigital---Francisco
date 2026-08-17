using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Emergency Management Agent responsible for multi-tiered action dispatch based on RiskLevel,
/// mock family notification broadcasting, emergency services dispatch countdown,
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
            throw new KeyNotFoundException($"Incident with ID '{incidentId}' was not found.");
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
            message: $"Evaluating action dispatch protocol for Incident #{incident.Id} (Risk Level: {incident.RiskLevel}).",
            details: $"Origin: {incident.Origin} | Description: '{incident.OriginalDescription}' | Contacts on file: {contacts.Count}",
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
                    Result = "General care guidance issued: Rest in a quiet environment, maintain hydration, and monitor vital signs. Non-urgent medical appointment request enabled."
                };
                dispatchedActions.Add(mildAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: "Mild risk protocol: General health recommendation generated. Non-urgent appointment option active.",
                    details: mildAction.Result,
                    incidentId: incident.Id
                );
                break;

            case RiskLevel.Urgent:
                // Action 1: Simulated Notification to Emergency Contacts
                var contactSummary = contacts.Count > 0
                    ? string.Join(", ", contacts.Select(c => $"{c.ContactName} [{c.Relationship}]: {c.Phone} ({c.PreferredMethod})"))
                    : "Primary family contacts (Default emergency list)";

                var urgentNotifyAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.NotifyFamily,
                    Timestamp = DateTime.UtcNow,
                    Result = $"Simulated emergency notification sent to contacts: {contactSummary}. Message: 'Urgent health alert for patient. Clinical accompaniment advised.'"
                };
                dispatchedActions.Add(urgentNotifyAction);

                // Action 2: Travel / Urgent Clinic Guidance
                var urgentTravelAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.GeneralRecommendation,
                    Timestamp = DateTime.UtcNow,
                    Result = "Clinical travel guidance: Proceed to nearest urgent care center or medical clinic with an accompanying person. Do not drive or walk alone if dizziness/weakness persists."
                };
                dispatchedActions.Add(urgentTravelAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: $"Urgent risk protocol: Broadcasted simulated notifications to {contacts.Count} contact(s) and issued clinic travel guidance.",
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
                    : "All registered emergency contacts";

                var emergencyNotifyAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.NotifyFamily,
                    Timestamp = DateTime.UtcNow,
                    Result = $"EMERGENCY PROTOCOL ACTIVATED: High-priority broadcast dispatched to ALL emergency contacts ({allContactsStr}). First responder rescue telemetry activated."
                };
                dispatchedActions.Add(emergencyNotifyAction);

                // Action 2: Contact Emergency Services (911 / EMS) with 30s Countdown
                var emergencyServicesAction = new ActionExecuted
                {
                    IncidentId = incident.Id,
                    ActionType = ActionType.ContactEmergencyServices,
                    Timestamp = DateTime.UtcNow,
                    Result = "EMERGENCY SERVICES DISPATCH: Automated 911 / EMS ambulance dispatch countdown initiated (30-second cancellation window). Rescue Medical Sheet exposed for first responders."
                };
                dispatchedActions.Add(emergencyServicesAction);

                _agentLogger.Log(
                    agentName: "EmergencyManagementAgent",
                    cycleStage: "Analysis",
                    message: "POSSIBLE EMERGENCY PROTOCOL TRIGGERED: Notified all emergency contacts and initiated 911 dispatch countdown.",
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
            message: $"Incident #{incident.Id} transitioned to 'ActionTaken'. {dispatchedActions.Count} automated actions executed.",
            details: $"Emergency Protocol: {emergencyProtocolActivated} | Countdown: {countdownSeconds?.ToString() ?? "N/A"}s",
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
            throw new KeyNotFoundException($"Incident with ID '{incidentId}' was not found.");
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
            PatientFullName: user?.FullName ?? "Unknown Patient",
            NationalId: user?.NationalId ?? "N/A",
            Age: age,
            Gender: user?.Gender ?? "Unknown",
            PrimaryPhone: user?.PrimaryPhone ?? "N/A",
            Address: user?.Address ?? "N/A",
            HealthInsurance: user?.HealthInsurance ?? "None specified",
            BloodType: user?.BloodType?.Value ?? "Unknown",
            KnownAllergies: medProfile?.KnownAllergies ?? new List<string>(),
            PreexistingConditions: medProfile?.PreexistingConditions ?? new List<string>(),
            CurrentMedication: medProfile?.CurrentMedication ?? new List<string>(),
            MedicalHistory: medProfile?.MedicalHistory ?? "No detailed history available",
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
            throw new KeyNotFoundException($"Incident with ID '{incidentId}' was not found.");
        }

        var appointmentAction = new ActionExecuted
        {
            IncidentId = incident.Id,
            ActionType = ActionType.RequestMedicalAppointment,
            Timestamp = DateTime.UtcNow,
            Result = $"Non-urgent medical appointment booked with primary care network. Follow-up notes: \"{notes ?? "Patient requested checkup following mild symptom report."}\""
        };

        _db.ActionsExecuted.Add(appointmentAction);
        await _db.SaveChangesAsync(cancellationToken);

        _agentLogger.Log(
            agentName: "EmergencyManagementAgent",
            cycleStage: "Decision",
            message: $"Medical appointment scheduled for Incident #{incident.Id}.",
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
