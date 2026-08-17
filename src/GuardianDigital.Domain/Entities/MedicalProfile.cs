namespace GuardianDigital.Domain.Entities;

public class MedicalProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string MedicalHistory { get; set; } = string.Empty;
    public List<string> CurrentMedication { get; set; } = new();
    public List<string> KnownAllergies { get; set; } = new();
    public List<string> PreexistingConditions { get; set; } = new();

    public User? User { get; set; }
}
