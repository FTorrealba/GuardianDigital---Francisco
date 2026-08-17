using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Domain.Entities;

public class EmergencyContact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public ContactPreferredMethod PreferredMethod { get; set; } = ContactPreferredMethod.Call;

    public User? User { get; set; }
}
