using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Domain.Entities;

public class Incident
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public IncidentOrigin Origin { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Detected;

    public User? User { get; set; }
    public List<UserResponse> UserResponses { get; set; } = new();
    public List<ActionExecuted> ActionsExecuted { get; set; } = new();
}
