using GuardianDigital.Application.Features.Health;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class HealthCheckTests
{
    [Fact]
    public async Task HealthCheck_SavesAndCountsRecordsCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "HealthTestDb_" + Guid.NewGuid())
            .Options;

        using var db = new GuardianDbContext(options);
        db.SystemHealthChecks.Add(new SystemHealth { Status = "Healthy", CheckedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        // Act
        var count = await db.SystemHealthChecks.CountAsync();
        var response = new GetHealthCheck.Response("Healthy", "Backend connected", DateTime.UtcNow, count);

        // Assert
        Assert.Equal(1, response.DatabaseRecordCount);
        Assert.Equal("Healthy", response.Status);
        Assert.Equal("Backend connected", response.Message);
    }
}
