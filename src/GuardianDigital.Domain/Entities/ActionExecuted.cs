using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Domain.Entities;

public class ActionExecuted
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IncidentId { get; set; }
    public ActionType ActionType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Result { get; set; } = string.Empty;

    public Incident? Incident { get; set; }
}
