using GuardianDigital.Domain.Exceptions;

namespace GuardianDigital.Domain.ValueObjects;

public record BloodType
{
    private static readonly HashSet<string> AllowedBloodTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"
    };

    public string Value { get; }

    public BloodType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Blood type cannot be empty.");
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (!AllowedBloodTypes.Contains(normalized))
        {
            throw new DomainException($"Invalid blood type '{value}'. Allowed values are: A+, A-, B+, B-, AB+, AB-, O+, O-.");
        }

        Value = normalized;
    }

    public static BloodType Create(string value) => new(value);

    public override string ToString() => Value;
}
