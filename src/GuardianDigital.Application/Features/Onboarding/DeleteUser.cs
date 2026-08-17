using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Onboarding;

public static class DeleteUser
{
    public static async Task<IResult> HandleAsync(Guid id, IGuardianDbContext db)
    {
        var user = await db.Users
            .Include(u => u.MedicalProfile)
            .Include(u => u.EmergencyContacts)
            .Include(u => u.LinkedDevices)
            .Include(u => u.Incidents)
                .ThenInclude(i => i.UserResponses)
            .Include(u => u.Incidents)
                .ThenInclude(i => i.ActionsExecuted)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Results.NotFound(new { error = $"User with ID '{id}' was not found." });
        }

        // Clean up linked sensor readings if any
        var deviceIds = user.LinkedDevices.Select(d => d.Id).ToList();
        if (deviceIds.Any())
        {
            var readings = await db.SensorReadings.Where(r => deviceIds.Contains(r.DeviceId)).ToListAsync();
            db.SensorReadings.RemoveRange(readings);
        }

        db.Users.Remove(user);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = $"Usuario '{user.FullName}' y su perfil han sido eliminados correctamente." });
    }
}
