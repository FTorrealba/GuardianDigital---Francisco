using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class GetUsers
{
    public static async Task<IResult> HandleAsync(IGuardianDbContext db)
    {
        var users = await db.Users
            .Include(u => u.MedicalProfile)
            .Include(u => u.EmergencyContacts)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var dtos = users.Select(CreateUser.ToDto).ToList();
        return Results.Ok(dtos);
    }
}
