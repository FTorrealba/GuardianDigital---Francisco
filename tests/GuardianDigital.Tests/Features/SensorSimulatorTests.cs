using GuardianDigital.Application.Features.Dashboard;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.ValueObjects;
using GuardianDigital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class SensorSimulatorTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "SensorTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    [Fact]
    public async Task SeedDevices_CreatesDefaultActiveDevicesForUser()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new User(
            "Juan Perez",
            "55443322D",
            new DateTime(1955, 6, 1),
            "Male",
            "+54911009988",
            "Avenida Cordoba 1234",
            new BloodType("A+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "C1", Relationship = "R1", Phone = "+111111" },
                new() { ContactName = "C2", Relationship = "R2", Phone = "+222222" },
                new() { ContactName = "C3", Relationship = "R3", Phone = "+333333" }
            }
        );
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        var result = await SeedDevices.HandleAsync(user.Id, db);
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        // Assert
        var devicesCount = await db.LinkedDevices.CountAsync(d => d.UserId == user.Id);
        Assert.Equal(6, devicesCount);
    }

    [Fact]
    public async Task InjectAnomaly_Fall_StoresOutofRangeSensorReadingInDatabase()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var user = new User(
            "Maria Lopez",
            "11224455E",
            new DateTime(1962, 8, 14),
            "Female",
            "+54911445566",
            "Calle Belgrano 789",
            new BloodType("O+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "C1", Relationship = "R1", Phone = "+111111" },
                new() { ContactName = "C2", Relationship = "R2", Phone = "+222222" },
                new() { ContactName = "C3", Relationship = "R3", Phone = "+333333" }
            }
        );
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await SeedDevices.HandleAsync(user.Id, db);

        // Act - Inject Fall Anomaly
        var request = new InjectAnomaly.InjectAnomalyRequest(null, "Fall");
        var result = await InjectAnomaly.HandleAsync(request, db);

        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        // Assert persistence
        var reading = await db.SensorReadings.OrderByDescending(r => r.Timestamp).FirstOrDefaultAsync();
        Assert.NotNull(reading);
        Assert.Equal(DataType.Motion, reading.DataType);
        Assert.Contains("FALL IMPACT", reading.Value);
    }
}
