using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Domain.Entities;

public class LinkedDevice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public DeviceType Type { get; set; }
    public DeviceStatus Status { get; set; } = DeviceStatus.Active;
    public DateTime? LastReading { get; set; }

    public User? User { get; set; }
    public List<SensorReading> Readings { get; set; } = new();
}
