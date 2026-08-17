using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Dashboard;

public static class SeedDevices
{
    public static async Task<IResult> HandleAsync(Guid? userId, IGuardianDbContext db)
    {
        var user = userId.HasValue
            ? await db.Users.Include(u => u.LinkedDevices).FirstOrDefaultAsync(u => u.Id == userId.Value)
            : await db.Users.Include(u => u.LinkedDevices).FirstOrDefaultAsync();

        if (user == null)
        {
            return Results.BadRequest(new { error = "No user found to seed devices. Please complete onboarding or register a user first." });
        }

        if (user.LinkedDevices.Any())
        {
            return Results.Ok(new { message = "User already has linked devices.", count = user.LinkedDevices.Count });
        }

        var defaultDevices = new List<LinkedDevice>
        {
            new() { UserId = user.Id, Type = DeviceType.BiometricBand, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow },
            new() { UserId = user.Id, Type = DeviceType.PulseOximeter, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow },
            new() { UserId = user.Id, Type = DeviceType.MotionSensor, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow },
            new() { UserId = user.Id, Type = DeviceType.DoorSensor, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow },
            new() { UserId = user.Id, Type = DeviceType.Camera, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow },
            new() { UserId = user.Id, Type = DeviceType.Microphone, Status = DeviceStatus.Active, LastReading = DateTime.UtcNow }
        };

        db.LinkedDevices.AddRange(defaultDevices);
        await db.SaveChangesAsync();

        return Results.Ok(new { message = "Default linked devices seeded successfully.", count = defaultDevices.Count });
    }
}
