using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.ValueObjects;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class EmergencyManagementTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "EmergencyTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    private EmergencyManagementService CreateEmergencyService(GuardianDbContext db, IAgentLogService? agentLogger = null)
    {
        agentLogger ??= new AgentLogService(NullLogger<AgentLogService>.Instance);
        return new EmergencyManagementService(db, agentLogger, NullLogger<EmergencyManagementService>.Instance);
    }

    private async Task<(User User, MedicalProfile Profile, Incident Incident)> SeedPatientWithIncidentAsync(
        GuardianDbContext db,
        RiskLevel riskLevel,
        IncidentStatus status = IncidentStatus.UnderEvaluation)
    {
        var user = new User(
            "Elena Vasquez",
            "30998877A",
            new DateTime(1945, 11, 23), // 80+ years old
            "Female",
            "+54911443322",
            "Calle Las Heras 1200",
            new BloodType("AB+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "Sofia Vasquez", Relationship = "Daughter", Phone = "+54911998877", PreferredMethod = ContactPreferredMethod.Call },
                new() { ContactName = "Pablo Vasquez", Relationship = "Son", Phone = "+54911887766", PreferredMethod = ContactPreferredMethod.SMS },
                new() { ContactName = "Dr. Alvarez", Relationship = "Physician", Phone = "+54911776655", PreferredMethod = ContactPreferredMethod.PushNotification }
            },
            "OSDE 410"
        );

        user.MedicalProfile = new MedicalProfile
        {
            UserId = user.Id,
            MedicalHistory = "Chronic Hypertension and Osteoporosis",
            CurrentMedication = new List<string> { "Amlodipine", "Calcium Vitamin D" },
            KnownAllergies = new List<string> { "Penicillin", "Sulfa" },
            PreexistingConditions = new List<string> { "Hypertension", "Osteoporosis" }
        };

        db.Users.Add(user);

        var incident = new Incident
        {
            UserId = user.Id,
            Origin = IncidentOrigin.Voice,
            OriginalDescription = "Acute chest discomfort with left arm radiation",
            RiskLevel = riskLevel,
            Status = status
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        return (user, user.MedicalProfile, incident);
    }

    [Fact]
    public async Task DispatchActions_Mild_GeneratesGeneralRecommendationAndActionTaken()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);
        var service = CreateEmergencyService(db, agentLogger);
        var (_, _, incident) = await SeedPatientWithIncidentAsync(db, RiskLevel.Mild);

        // Act
        var result = await service.DispatchActionsAsync(incident.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ActionTaken", result.Status);
        Assert.False(result.EmergencyProtocolActivated);
        Assert.Single(result.ActionsDispatched);
        Assert.Equal("GeneralRecommendation", result.ActionsDispatched[0].ActionType);

        var dbIncident = await db.Incidents.Include(i => i.ActionsExecuted).FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(dbIncident);
        Assert.Equal(IncidentStatus.ActionTaken, dbIncident.Status);
        Assert.Single(dbIncident.ActionsExecuted);

        var logs = agentLogger.GetLogs();
        Assert.Contains(logs, l => l.AgentName == "EmergencyManagementAgent" && l.CycleStage == "Decision");
    }

    [Fact]
    public async Task DispatchActions_Urgent_NotifiesContactsAndRecommendsTravel()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateEmergencyService(db);
        var (_, _, incident) = await SeedPatientWithIncidentAsync(db, RiskLevel.Urgent);

        // Act
        var result = await service.DispatchActionsAsync(incident.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ActionTaken", result.Status);
        Assert.False(result.EmergencyProtocolActivated);
        Assert.Equal(2, result.ActionsDispatched.Count);
        Assert.Contains(result.ActionsDispatched, a => a.ActionType == "NotifyFamily");
        Assert.Contains(result.ActionsDispatched, a => a.ActionType == "GeneralRecommendation");

        var notifyAction = result.ActionsDispatched.First(a => a.ActionType == "NotifyFamily");
        Assert.Contains("Sofia Vasquez", notifyAction.Result);
        Assert.Contains("Pablo Vasquez", notifyAction.Result);
    }

    [Fact]
    public async Task DispatchActions_PossibleEmergency_NotifiesAllContactsAndDispatchesEmergencyServices()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateEmergencyService(db);
        var (_, _, incident) = await SeedPatientWithIncidentAsync(db, RiskLevel.PossibleEmergency);

        // Act
        var result = await service.DispatchActionsAsync(incident.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ActionTaken", result.Status);
        Assert.True(result.EmergencyProtocolActivated);
        Assert.Equal(30, result.DispatchCountdownSeconds);
        Assert.Equal(2, result.ActionsDispatched.Count);
        Assert.Contains(result.ActionsDispatched, a => a.ActionType == "NotifyFamily");
        Assert.Contains(result.ActionsDispatched, a => a.ActionType == "ContactEmergencyServices");

        var emergencyAction = result.ActionsDispatched.First(a => a.ActionType == "ContactEmergencyServices");
        Assert.Contains("30-second cancellation window", emergencyAction.Result);
    }

    [Fact]
    public async Task GetRescueSheet_ReturnsCriticalFirstResponderData()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateEmergencyService(db);
        var (user, _, incident) = await SeedPatientWithIncidentAsync(db, RiskLevel.PossibleEmergency);

        // Act
        var httpResult = await GetRescueSheet.HandleAsync(incident.Id, service);

        // Assert
        var okResult = Assert.IsType<Ok<RescueSheetDto>>(httpResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("Elena Vasquez", okResult.Value.PatientFullName);
        Assert.Equal("AB+", okResult.Value.BloodType);
        Assert.Contains("Penicillin", okResult.Value.KnownAllergies);
        Assert.Contains("Hypertension", okResult.Value.PreexistingConditions);
        Assert.Contains("Amlodipine", okResult.Value.CurrentMedication);
        Assert.Equal(3, okResult.Value.EmergencyContacts.Count);
        Assert.Equal("OSDE 410", okResult.Value.HealthInsurance);
    }

    [Fact]
    public async Task RequestMedicalAppointment_CreatesActionExecutedRecord()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var service = CreateEmergencyService(db);
        var (_, _, incident) = await SeedPatientWithIncidentAsync(db, RiskLevel.Mild);

        var request = new RequestAppointment.RequestAppointmentRequest(Notes: "Check mild knee discomfort");

        // Act
        var httpResult = await RequestAppointment.HandleAsync(incident.Id, request, service);

        // Assert
        var okResult = Assert.IsType<Ok<DispatchedActionSummary>>(httpResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("RequestMedicalAppointment", okResult.Value.ActionType);
        Assert.Contains("Check mild knee discomfort", okResult.Value.Result);

        var dbIncident = await db.Incidents.Include(i => i.ActionsExecuted).FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(dbIncident);
        Assert.Contains(dbIncident.ActionsExecuted, a => a.ActionType == ActionType.RequestMedicalAppointment);
    }
}
