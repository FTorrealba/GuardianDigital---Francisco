using System.Text.RegularExpressions;
using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.Exceptions;
using GuardianDigital.Domain.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class CreateUser
{
    public record CreateUserRequest(
        string FullName,
        string NationalId,
        DateTime DateOfBirth,
        string Gender,
        string PrimaryPhone,
        string Address,
        string? HealthInsurance,
        string BloodType,
        MedicalProfileRequest? MedicalProfile,
        List<EmergencyContactRequest> EmergencyContacts
    );

    public record MedicalProfileRequest(
        string MedicalHistory,
        List<string>? CurrentMedication,
        List<string>? KnownAllergies,
        List<string>? PreexistingConditions
    );

    public record EmergencyContactRequest(
        string ContactName,
        string Relationship,
        string Phone,
        string PreferredMethod
    );

    public record UserResponseDto(
        Guid Id,
        string FullName,
        string NationalId,
        DateTime DateOfBirth,
        string Gender,
        string PrimaryPhone,
        string Address,
        string? HealthInsurance,
        string BloodType,
        MedicalProfileResponseDto? MedicalProfile,
        List<EmergencyContactResponseDto> EmergencyContacts
    );

    public record MedicalProfileResponseDto(
        Guid Id,
        string MedicalHistory,
        List<string> CurrentMedication,
        List<string> KnownAllergies,
        List<string> PreexistingConditions
    );

    public record EmergencyContactResponseDto(
        Guid Id,
        string ContactName,
        string Relationship,
        string Phone,
        string PreferredMethod
    );

    public static bool IsValidDni(string dni)
    {
        if (string.IsNullOrWhiteSpace(dni)) return false;
        var trimmed = dni.Trim();
        return trimmed.Length >= 1 && trimmed.Length <= 9 && trimmed.All(char.IsDigit);
    }

    public static bool IsValidPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 8;
    }

    public static async Task<IResult> HandleAsync(CreateUserRequest request, IGuardianDbContext db)
    {
        // 1. Validation: DNI format (positive natural number up to 9 digits, no decimals/negatives)
        if (!IsValidDni(request.NationalId))
        {
            return Results.BadRequest(new { error = "El Documento de Identidad (DNI) debe contener solo números naturales y tener un máximo de 9 dígitos." });
        }

        // 2. Validation: Phone format
        if (!IsValidPhone(request.PrimaryPhone))
        {
            return Results.BadRequest(new { error = "El teléfono principal debe contener el prefijo +549 y exactamente 8 dígitos numéricos." });
        }

        // 3. Validation: Minimum 3 emergency contacts
        if (request.EmergencyContacts == null || request.EmergencyContacts.Count < 3)
        {
            return Results.BadRequest(new { error = "A user must have at least 3 emergency contacts." });
        }

        foreach (var contact in request.EmergencyContacts)
        {
            if (!IsValidPhone(contact.Phone))
            {
                return Results.BadRequest(new { error = $"Emergency contact phone '{contact.Phone}' format is invalid." });
            }
        }

        // 4. Validation: Unique National ID
        var exists = await db.Users.AnyAsync(u => u.NationalId.ToLower() == request.NationalId.Trim().ToLower());
        if (exists)
        {
            return Results.BadRequest(new { error = $"A user with National ID '{request.NationalId}' is already registered." });
        }

        // 4. Value Object & Domain aggregate creation
        BloodType bloodType;
        try
        {
            bloodType = new BloodType(request.BloodType);
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        var contactsList = request.EmergencyContacts.Select(c =>
        {
            if (!Enum.TryParse<ContactPreferredMethod>(c.PreferredMethod, true, out var method))
            {
                method = ContactPreferredMethod.Call;
            }

            return new EmergencyContact
            {
                ContactName = c.ContactName,
                Relationship = c.Relationship,
                Phone = c.Phone,
                PreferredMethod = method
            };
        }).ToList();

        User user;
        try
        {
            user = new User(
                fullName: request.FullName,
                nationalId: request.NationalId.Trim(),
                dateOfBirth: request.DateOfBirth,
                gender: request.Gender,
                primaryPhone: request.PrimaryPhone,
                address: request.Address,
                bloodType: bloodType,
                emergencyContacts: contactsList,
                healthInsurance: request.HealthInsurance
            );
        }
        catch (DomainException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        // Medical profile creation
        if (request.MedicalProfile != null)
        {
            user.MedicalProfile = new MedicalProfile
            {
                UserId = user.Id,
                MedicalHistory = request.MedicalProfile.MedicalHistory ?? string.Empty,
                CurrentMedication = request.MedicalProfile.CurrentMedication ?? new List<string>(),
                KnownAllergies = request.MedicalProfile.KnownAllergies ?? new List<string>(),
                PreexistingConditions = request.MedicalProfile.PreexistingConditions ?? new List<string>()
            };
        }

        // Single EF Core transaction execution
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var dto = ToDto(user);
        return Results.Created($"/api/users/{user.Id}", dto);
    }

    public static UserResponseDto ToDto(User u)
    {
        return new UserResponseDto(
            u.Id,
            u.FullName,
            u.NationalId,
            u.DateOfBirth,
            u.Gender,
            u.PrimaryPhone,
            u.Address,
            u.HealthInsurance,
            u.BloodType.Value,
            u.MedicalProfile == null ? null : new MedicalProfileResponseDto(
                u.MedicalProfile.Id,
                u.MedicalProfile.MedicalHistory,
                u.MedicalProfile.CurrentMedication,
                u.MedicalProfile.KnownAllergies,
                u.MedicalProfile.PreexistingConditions
            ),
            u.EmergencyContacts.Select(c => new EmergencyContactResponseDto(
                c.Id,
                c.ContactName,
                c.Relationship,
                c.Phone,
                c.PreferredMethod.ToString()
            )).ToList()
        );
    }
}
