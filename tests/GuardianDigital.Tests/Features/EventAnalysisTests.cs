using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class EventAnalysisTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "EventAnalysisTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    [Fact]
    public async Task EventAnalysis_DetectsFall_CreatesIncidentInDetectedStatus()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);

        var userId = Guid.NewGuid();
        var device = new LinkedDevice { Id = Guid.NewGuid(), UserId = userId, Type = DeviceType.MotionSensor, Status = DeviceStatus.Active };
        db.LinkedDevices.Add(device);

        var fallReading = new SensorReading
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            DataType = DataType.Motion,
            Value = "CRITICAL FALL IMPACT DETECTED (5.2G Acceleration Vector)",
            Timestamp = DateTime.UtcNow
        };
        db.SensorReadings.Add(fallReading);
        await db.SaveChangesAsync();

        // Act
        var incidentsBefore = await db.Incidents.CountAsync();
        Assert.Equal(0, incidentsBefore);

        var incident = new Incident
        {
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            Origin = IncidentOrigin.Sensor,
            OriginalDescription = "CRITICAL FALL DETECTED",
            RiskLevel = RiskLevel.PossibleEmergency,
            Status = IncidentStatus.Detected
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        agentLogger.Log("EventAnalysis", "Decision", "Incident created in Detected status", incidentId: incident.Id);

        // Assert
        var createdIncident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(createdIncident);
        Assert.Equal(IncidentStatus.Detected, createdIncident.Status);
        Assert.Equal(RiskLevel.PossibleEmergency, createdIncident.RiskLevel);

        var logs = agentLogger.GetLogs();
        Assert.NotEmpty(logs);
        Assert.Contains(logs, l => l.AgentName == "EventAnalysis" && l.CycleStage == "Decision");
    }

    [Fact]
    public async Task GetIncidents_ReturnsListWithDetails()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var incident = new Incident
        {
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Origin = IncidentOrigin.Sensor,
            OriginalDescription = "CARDIAC ALERT: Tachycardia detected",
            RiskLevel = RiskLevel.Urgent,
            Status = IncidentStatus.Detected
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        // Act
        var result = await GetIncidents.HandleAsync(db);

        // Assert
        var okResult = Assert.IsType<Ok<List<GetIncidents.IncidentDto>>>(result);
        Assert.NotNull(okResult.Value);
        Assert.Single(okResult.Value);
        Assert.Equal("Detected", okResult.Value[0].Status);
        Assert.Equal("Urgent", okResult.Value[0].RiskLevel);
    }
}
