using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.ValueObjects;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class LearningStatsTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "LearningTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    private LearningStatsService CreateLearningService(GuardianDbContext db, IAgentLogService? agentLogger = null)
    {
        agentLogger ??= new AgentLogService(NullLogger<AgentLogService>.Instance);
        return new LearningStatsService(db, agentLogger, NullLogger<LearningStatsService>.Instance);
    }

    [Fact]
    public async Task MarkFalseAlarm_UpdatesIncidentStatusAndLogsFeedback()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Origin = IncidentOrigin.Sensor,
            OriginalDescription = "FALL IMPACT DETECTED (5.2G Acceleration)",
            RiskLevel = RiskLevel.PossibleEmergency,
            Status = IncidentStatus.Detected,
            Timestamp = DateTime.UtcNow
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        var request = new MarkFalseAlarm.MarkFalseAlarmRequest("Device accidentally dropped on floor by caregiver");

        // Act
        var httpResult = await MarkFalseAlarm.HandleAsync(incident.Id, request, db, agentLogger);

        // Assert HTTP
        var okResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(httpResult);
        Assert.Equal(200, okResult.StatusCode);

        // Assert DB
        var updatedIncident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(updatedIncident);
        Assert.Equal(IncidentStatus.FalseAlarm, updatedIncident.Status);
        Assert.Contains("Device accidentally dropped", updatedIncident.OriginalDescription);

        // Assert Logs
        var logs = agentLogger.GetLogs();
        Assert.Contains(logs, l => l.AgentName == "LearningAgent" && l.Message.Contains("FalseAlarm"));
    }

    [Fact]
    public async Task GetLearningStats_ComputesFalseAlarmRateAccurately()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateLearningService(db);
        var userId = Guid.NewGuid();

        // Add 8 genuine incidents
        for (int i = 0; i < 8; i++)
        {
            db.Incidents.Add(new Incident
            {
                UserId = userId,
                Origin = IncidentOrigin.Sensor,
                OriginalDescription = $"Sensor anomaly #{i}",
                RiskLevel = RiskLevel.Urgent,
                Status = IncidentStatus.Closed,
                Timestamp = DateTime.UtcNow.AddDays(-i)
            });
        }

        // Add 2 false alarms in the last 30 days
        for (int i = 0; i < 2; i++)
        {
            db.Incidents.Add(new Incident
            {
                UserId = userId,
                Origin = IncidentOrigin.Voice,
                OriginalDescription = $"False alarm symptom #{i}",
                RiskLevel = RiskLevel.Mild,
                Status = IncidentStatus.FalseAlarm,
                Timestamp = DateTime.UtcNow.AddDays(-i)
            });
        }

        // Add 1 old false alarm from 40 days ago (should be excluded from 30-day rate)
        db.Incidents.Add(new Incident
        {
            UserId = userId,
            Origin = IncidentOrigin.Text,
            OriginalDescription = "Old false alarm from 40 days ago",
            RiskLevel = RiskLevel.Mild,
            Status = IncidentStatus.FalseAlarm,
            Timestamp = DateTime.UtcNow.AddDays(-40)
        });

        await db.SaveChangesAsync();

        // Act
        var stats = await service.GetLearningStatsAsync(userId);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(10, stats.TotalIncidentsLast30Days); // 8 genuine + 2 false alarms in 30d
        Assert.Equal(2, stats.FalseAlarmsLast30Days);
        Assert.Equal(20.0, stats.FalseAlarmPercentage); // 2/10 = 20%
        Assert.Equal(80.0, stats.PrecisionScore);
    }

    [Fact]
    public async Task GetLearningStats_AggregatesIncidentsByOriginAndRisk()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateLearningService(db);
        var userId = Guid.NewGuid();

        db.Incidents.Add(new Incident { UserId = userId, Origin = IncidentOrigin.Sensor, RiskLevel = RiskLevel.PossibleEmergency, Status = IncidentStatus.ActionTaken });
        db.Incidents.Add(new Incident { UserId = userId, Origin = IncidentOrigin.Sensor, RiskLevel = RiskLevel.Urgent, Status = IncidentStatus.UnderEvaluation });
        db.Incidents.Add(new Incident { UserId = userId, Origin = IncidentOrigin.Voice, RiskLevel = RiskLevel.PossibleEmergency, Status = IncidentStatus.Detected });
        db.Incidents.Add(new Incident { UserId = userId, Origin = IncidentOrigin.Text, RiskLevel = RiskLevel.Mild, Status = IncidentStatus.Closed });
        await db.SaveChangesAsync();

        // Act
        var stats = await service.GetLearningStatsAsync(userId);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.IncidentsByOrigin["Sensor"]);
        Assert.Equal(1, stats.IncidentsByOrigin["Voice"]);
        Assert.Equal(1, stats.IncidentsByOrigin["Text"]);

        Assert.Equal(2, stats.IncidentsByRiskLevel["PossibleEmergency"]);
        Assert.Equal(1, stats.IncidentsByRiskLevel["Urgent"]);
        Assert.Equal(1, stats.IncidentsByRiskLevel["Mild"]);
    }

    [Fact]
    public async Task GetLearningStats_ComputesTypicalActivityHoursFromSensorReadings()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateLearningService(db);
        var deviceId = Guid.NewGuid();

        var today = DateTime.UtcNow.Date;

        // Seed 10 readings at 10:00 AM (Hour 10)
        for (int i = 0; i < 10; i++)
        {
            db.SensorReadings.Add(new SensorReading
            {
                DeviceId = deviceId,
                DataType = DataType.Motion,
                Value = "Movement",
                Timestamp = today.AddHours(10).AddMinutes(i * 5)
            });
        }

        // Seed 2 readings at 03:00 AM (Hour 3)
        for (int i = 0; i < 2; i++)
        {
            db.SensorReadings.Add(new SensorReading
            {
                DeviceId = deviceId,
                DataType = DataType.Motion,
                Value = "Resting",
                Timestamp = today.AddHours(3).AddMinutes(i * 10)
            });
        }

        await db.SaveChangesAsync();

        // Act
        var stats = await service.GetLearningStatsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(24, stats.HourlyActivityDistribution.Count);

        var hour10 = stats.HourlyActivityDistribution.First(h => h.Hour == 10);
        Assert.Equal(10, hour10.ReadingCount);
        Assert.Equal("Peak", hour10.ActivityCategory);

        var hour3 = stats.HourlyActivityDistribution.First(h => h.Hour == 3);
        Assert.Equal(2, hour3.ReadingCount);
        Assert.Equal("Low", hour3.ActivityCategory);
    }
}
