namespace GuardianDigital.Domain.Entities;

public class SystemHealth
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Status { get; set; } = "Healthy";
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
