using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.Exceptions;
using GuardianDigital.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class UpdateUserProfile
{
    public record UpdateUserRequest(
        string FullName,
        string NationalId,
        DateTime DateOfBirth,
        string Gender,
        string PrimaryPhone,
        string Address,
        string BloodType,
        string? HealthInsurance,
        CreateUser.MedicalProfileRequest? MedicalProfile,
        List<CreateUser.EmergencyContactRequest> EmergencyContacts
    );

    public static async Task<IResult> HandleAsync(Guid id, UpdateUserRequest request, IGuardianDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { error = "FullName is required." });
        }

        if (!CreateUser.IsValidDni(request.NationalId))
        {
            return Results.BadRequest(new { error = "El Documento de Identidad (DNI) debe contener solo números naturales y tener un máximo de 9 dígitos." });
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.BadRequest(new { error = "Address is required." });
        }

        if (!CreateUser.IsValidPhone(request.PrimaryPhone))
        {
            return Results.BadRequest(new { error = "El teléfono principal debe contener el prefijo +549 y exactamente 10 dígitos numéricos." });
        }

        BloodType bloodType;
        try
        {
            bloodType = BloodType.Create(request.BloodType);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        if (request.EmergencyContacts == null || request.EmergencyContacts.Count < 3)
        {
            return Results.BadRequest(new { error = "A user must have at least 3 emergency contacts." });
        }

        foreach (var contact in request.EmergencyContacts)
        {
            if (string.IsNullOrWhiteSpace(contact.ContactName))
            {
                return Results.BadRequest(new { error = "ContactName cannot be empty for all emergency contacts." });
            }

            if (!CreateUser.IsValidPhone(contact.Phone))
            {
                return Results.BadRequest(new { error = $"Emergency contact phone '{contact.Phone}' format is invalid." });
            }
        }

        var user = await db.Users
            .Include(u => u.MedicalProfile)
            .Include(u => u.EmergencyContacts)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Results.NotFound(new { error = $"User with ID '{id}' was not found." });
        }

        // Update Personal Info
        user.FullName = request.FullName.Trim();
        user.NationalId = request.NationalId.Trim();
        user.DateOfBirth = request.DateOfBirth;
        user.Gender = request.Gender.Trim();
        user.PrimaryPhone = request.PrimaryPhone.Trim();
        user.Address = request.Address.Trim();
        user.BloodType = bloodType;
        user.HealthInsurance = request.HealthInsurance?.Trim();

        // Update Medical Profile
        if (request.MedicalProfile != null)
        {
            if (user.MedicalProfile == null)
            {
                user.MedicalProfile = new MedicalProfile
                {
                    UserId = user.Id
                };
            }

            user.MedicalProfile.MedicalHistory = request.MedicalProfile.MedicalHistory ?? string.Empty;
            user.MedicalProfile.CurrentMedication = request.MedicalProfile.CurrentMedication ?? new List<string>();
            user.MedicalProfile.KnownAllergies = request.MedicalProfile.KnownAllergies ?? new List<string>();
            user.MedicalProfile.PreexistingConditions = request.MedicalProfile.PreexistingConditions ?? new List<string>();
        }

        // Update Emergency Contacts
        var newContacts = request.EmergencyContacts.Select(c =>
        {
            if (!Enum.TryParse<ContactPreferredMethod>(c.PreferredMethod, true, out var method))
            {
                method = ContactPreferredMethod.Call;
            }

            return new EmergencyContact
            {
                ContactName = c.ContactName.Trim(),
                Relationship = c.Relationship.Trim(),
                Phone = c.Phone.Trim(),
                PreferredMethod = method
            };
        }).ToList();

        try
        {
            user.SetEmergencyContacts(newContacts);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        await db.SaveChangesAsync();

        var dto = CreateUser.ToDto(user);
        return Results.Ok(dto);
    }
}
