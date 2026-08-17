using GuardianDigital.Domain.Enums;

namespace GuardianDigital.Domain.Entities;

public class SensorReading
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public DataType DataType { get; set; }
    public string Value { get; set; } = string.Empty;

    public LinkedDevice? Device { get; set; }
}
