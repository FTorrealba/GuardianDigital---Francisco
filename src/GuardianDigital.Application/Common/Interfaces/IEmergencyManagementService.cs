using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Application.Common.Interfaces;

public record EmergencyDispatchResult(
    Guid IncidentId,
    RiskLevel RiskLevel,
    string Status,
    IReadOnlyList<DispatchedActionSummary> ActionsDispatched,
    bool EmergencyProtocolActivated,
    int? DispatchCountdownSeconds,
    DateTime DispatchedAt
);

public record DispatchedActionSummary(
    Guid Id,
    string ActionType,
    string Result,
    DateTime Timestamp
);

public record RescueSheetDto(
    Guid IncidentId,
    string IncidentOrigin,
    string IncidentRiskLevel,
    string IncidentDescription,
    DateTime IncidentTimestamp,
    Guid PatientId,
    string PatientFullName,
    string NationalId,
    int Age,
    string Gender,
    string PrimaryPhone,
    string Address,
    string HealthInsurance,
    string BloodType,
    IReadOnlyList<string> KnownAllergies,
    IReadOnlyList<string> PreexistingConditions,
    IReadOnlyList<string> CurrentMedication,
    string MedicalHistory,
    IReadOnlyList<RescueContactDto> EmergencyContacts,
    DateTime GeneratedAt
);

public record RescueContactDto(
    string ContactName,
    string Relationship,
    string Phone,
    string PreferredMethod
);

public interface IEmergencyManagementService
{
    /// <summary>
    /// Evaluates the incident's RiskLevel and executes automated action dispatch:
    /// - Mild: General recommendation + enables medical appointment request
    /// - Urgent: Simulated family notifications + travel/urgent care recommendations
    /// - PossibleEmergency: Emergency protocol (notifies ALL contacts + emergency services dispatch countdown + exposes rescue sheet)
    /// </summary>
    Task<EmergencyDispatchResult> DispatchActionsAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the critical Rescue Medical Sheet without heavy authentication for fast first responder / paramedic access.
    /// </summary>
    Task<RescueSheetDto> GetRescueSheetAsync(
        Guid incidentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a non-urgent medical appointment request for mild incidents.
    /// </summary>
    Task<DispatchedActionSummary> RequestMedicalAppointmentAsync(
        Guid incidentId,
        string? notes = null,
        CancellationToken cancellationToken = default);
}
