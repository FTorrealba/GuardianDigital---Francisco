using GuardianDigital.Domain.Exceptions;
using GuardianDigital.Domain.ValueObjects;

namespace GuardianDigital.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string PrimaryPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? HealthInsurance { get; set; }
    public BloodType BloodType { get; set; } = null!;

    public MedicalProfile? MedicalProfile { get; set; }

    private readonly List<EmergencyContact> _emergencyContacts = new();
    public virtual IReadOnlyCollection<EmergencyContact> EmergencyContacts => _emergencyContacts.AsReadOnly();

    public List<LinkedDevice> LinkedDevices { get; set; } = new();
    public List<Incident> Incidents { get; set; } = new();

    public User() { }

    public User(
        string fullName,
        string nationalId,
        DateTime dateOfBirth,
        string gender,
        string primaryPhone,
        string address,
        BloodType bloodType,
        IEnumerable<EmergencyContact> emergencyContacts,
        string? healthInsurance = null)
    {
        FullName = fullName;
        NationalId = nationalId;
        DateOfBirth = dateOfBirth;
        Gender = gender;
        PrimaryPhone = primaryPhone;
        Address = address;
        BloodType = bloodType;
        HealthInsurance = healthInsurance;

        SetEmergencyContacts(emergencyContacts);
    }

    public void SetEmergencyContacts(IEnumerable<EmergencyContact> contacts)
    {
        var list = contacts?.ToList() ?? new List<EmergencyContact>();
        if (list.Count < 3)
        {
            throw new DomainException("A user must have at least 3 emergency contacts.");
        }

        _emergencyContacts.Clear();
        foreach (var c in list)
        {
            c.UserId = Id;
            _emergencyContacts.Add(c);
        }
    }

    public void AddEmergencyContact(EmergencyContact contact)
    {
        contact.UserId = Id;
        _emergencyContacts.Add(contact);
    }

    public void RemoveEmergencyContact(EmergencyContact contact)
    {
        if (_emergencyContacts.Count <= 3)
        {
            throw new DomainException("Cannot remove contact. A user must have at least 3 emergency contacts.");
        }
        _emergencyContacts.Remove(contact);
    }

    public void Validate()
    {
        if (_emergencyContacts.Count < 3)
        {
            throw new DomainException("A user must have at least 3 emergency contacts.");
        }
    }
}
