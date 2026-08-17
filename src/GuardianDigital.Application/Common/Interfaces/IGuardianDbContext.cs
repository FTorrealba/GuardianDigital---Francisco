using GuardianDigital.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Common.Interfaces;

public interface IGuardianDbContext
{
    DbSet<SystemHealth> SystemHealthChecks { get; }
    DbSet<User> Users { get; }
    DbSet<MedicalProfile> MedicalProfiles { get; }
    DbSet<EmergencyContact> EmergencyContacts { get; }
    DbSet<LinkedDevice> LinkedDevices { get; }
    DbSet<Incident> Incidents { get; }
    DbSet<UserResponse> UserResponses { get; }
    DbSet<ActionExecuted> ActionsExecuted { get; }
    DbSet<SensorReading> SensorReadings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
