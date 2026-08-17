using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class UpdateEmergencyContacts
{
    public record UpdateContactsRequest(
        List<CreateUser.EmergencyContactRequest> Contacts
    );

    public static async Task<IResult> HandleAsync(Guid id, UpdateContactsRequest request, IGuardianDbContext db)
    {
        if (request.Contacts == null || request.Contacts.Count < 3)
        {
            return Results.BadRequest(new { error = "A user must have at least 3 emergency contacts." });
        }

        foreach (var contact in request.Contacts)
        {
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

        var newContacts = request.Contacts.Select(c =>
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
