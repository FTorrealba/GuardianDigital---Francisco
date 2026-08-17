using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class GetUserById
{
    public static async Task<IResult> HandleAsync(Guid id, IGuardianDbContext db)
    {
        var user = await db.Users
            .Include(u => u.MedicalProfile)
            .Include(u => u.EmergencyContacts)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Results.NotFound(new { error = $"User with ID '{id}' was not found." });
        }

        var dto = CreateUser.ToDto(user);
        return Results.Ok(dto);
    }
}
