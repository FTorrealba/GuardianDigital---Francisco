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

public class RiskEvaluationTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "RiskEvaluationTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    private RiskEvaluationService CreateRiskEvaluator(GuardianDbContext db, IAgentLogService? agentLogger = null)
    {
        agentLogger ??= new AgentLogService(NullLogger<AgentLogService>.Instance);
        return new RiskEvaluationService(db, agentLogger, NullLogger<RiskEvaluationService>.Instance);
    }

    [Fact]
    public void HardRule_ChestPainWithRadiation_SetsPossibleEmergency_RegardlessOfBaseline()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var evaluator = CreateRiskEvaluator(db);

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            Origin = IncidentOrigin.Voice,
            OriginalDescription = "Reporte: 'Siento dolor de pecho que se irradia a mi brazo izquierdo y cuello' | Síntomas detectados: Dolor de pecho",
            RiskLevel = RiskLevel.Mild, // Baseline is mild
            Status = IncidentStatus.Detected
        };

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Carlos Alberto",
            DateOfBirth = new DateTime(1965, 2, 10)
        };

        // Act
        var result = evaluator.EvaluateRisk(incident, null, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RiskLevel.PossibleEmergency, result.FinalRiskLevel);
        Assert.True(result.HardRuleTriggered);
        Assert.Contains("ChestPainRadiation", result.AppliedRuleOrCriteria);
    }

    [Fact]
    public void HardRule_LossOfConsciousness_SetsPossibleEmergency()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var evaluator = CreateRiskEvaluator(db);

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            Origin = IncidentOrigin.Voice,
            OriginalDescription = "I had a sudden blackout and loss of consciousness on the floor",
            RiskLevel = RiskLevel.Urgent,
            Status = IncidentStatus.Detected
        };

        // Act
        var result = evaluator.EvaluateRisk(incident, null, null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RiskLevel.PossibleEmergency, result.FinalRiskLevel);
        Assert.True(result.HardRuleTriggered);
        Assert.Contains("LossOfConsciousness", result.AppliedRuleOrCriteria);
    }

    [Fact]
    public void Prioritization_DiabeticElderlyPatientWithDizziness_EscalatesMildToUrgent()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var evaluator = CreateRiskEvaluator(db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Elena Rossi",
            DateOfBirth = new DateTime(1942, 5, 12) // 84 years old
        };

        var medProfile = new MedicalProfile
        {
            UserId = user.Id,
            MedicalHistory = "Type 2 Diabetes",
            CurrentMedication = new List<string> { "Metformin 850mg" },
            PreexistingConditions = new List<string> { "Diabetes" }
        };

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Origin = IncidentOrigin.Text,
            OriginalDescription = "I feel dizzy and lightheaded while sitting in the living room",
            RiskLevel = RiskLevel.Mild, // Baseline is mild
            Status = IncidentStatus.Detected
        };

        // Act
        var result = evaluator.EvaluateRisk(incident, medProfile, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RiskLevel.Urgent, result.FinalRiskLevel);
        Assert.False(result.HardRuleTriggered);
        Assert.Contains(result.PrioritizationFactors, f => f.Contains("Diabetic", StringComparison.OrdinalIgnoreCase) || f.Contains("diabéti", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.PrioritizationFactors, f => f.Contains("Age", StringComparison.OrdinalIgnoreCase) || f.Contains("Consciousness", StringComparison.OrdinalIgnoreCase) || f.Contains("Edad", StringComparison.OrdinalIgnoreCase) || f.Contains("Conciencia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prioritization_AnticoagulantPatientWithFall_EscalatesToPossibleEmergency()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var evaluator = CreateRiskEvaluator(db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Jorge Santos",
            DateOfBirth = new DateTime(1950, 8, 20) // 76 years old
        };

        var medProfile = new MedicalProfile
        {
            UserId = user.Id,
            MedicalHistory = "Atrial Fibrillation",
            CurrentMedication = new List<string> { "Warfarin", "Aspirin" },
            PreexistingConditions = new List<string> { "Arrhythmia" }
        };

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Origin = IncidentOrigin.Voice,
            OriginalDescription = "I slipped and fell on the bathroom floor, bumped my head",
            RiskLevel = RiskLevel.Urgent,
            Status = IncidentStatus.Detected
        };

        // Act
        var result = evaluator.EvaluateRisk(incident, medProfile, user);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(RiskLevel.PossibleEmergency, result.FinalRiskLevel);
        Assert.Contains(result.PrioritizationFactors, f => f.Contains("Anticoagulant", StringComparison.OrdinalIgnoreCase) || f.Contains("Anticoagulante", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateIncidentRiskAsync_UpdatesIncidentStatusToUnderEvaluation()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);
        var evaluator = CreateRiskEvaluator(db, agentLogger);

        var user = new User(
            "Marta Gomez",
            "44332211C",
            new DateTime(1948, 3, 15),
            "Female",
            "+54911223344",
            "Avenida Libertador 123",
            new BloodType("A+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "Daughter", Relationship = "Daughter", Phone = "+111111" },
                new() { ContactName = "Son", Relationship = "Son", Phone = "+222222" },
                new() { ContactName = "Neighbor", Relationship = "Neighbor", Phone = "+333333" }
            }
        );
        db.Users.Add(user);

        var medProfile = new MedicalProfile
        {
            UserId = user.Id,
            MedicalHistory = "Hypertension",
            CurrentMedication = new List<string> { "Losartan" },
            PreexistingConditions = new List<string> { "High Blood Pressure" }
        };
        db.MedicalProfiles.Add(medProfile);

        var incident = new Incident
        {
            UserId = user.Id,
            Origin = IncidentOrigin.Voice,
            OriginalDescription = "Chest pain with radiation to the left arm and shortness of breath",
            RiskLevel = RiskLevel.Mild, // Initially logged as mild
            Status = IncidentStatus.Detected
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        // Act
        var httpResult = await EvaluateMedicalRisk.HandleAsync(incident.Id, evaluator);

        // Assert HTTP response
        var okResult = Assert.IsType<Ok<EvaluateMedicalRisk.EvaluateMedicalRiskResponse>>(httpResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("UnderEvaluation", okResult.Value.Status);
        Assert.Equal("PossibleEmergency", okResult.Value.FinalRiskLevel);
        Assert.True(okResult.Value.HardRuleTriggered);

        // Assert Database State
        var updatedIncident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(updatedIncident);
        Assert.Equal(IncidentStatus.UnderEvaluation, updatedIncident.Status);
        Assert.Equal(RiskLevel.PossibleEmergency, updatedIncident.RiskLevel);

        // Assert Agent Logs
        var logs = agentLogger.GetLogs();
        Assert.Contains(logs, l => l.AgentName == "MedicalEvaluationAgent" && l.CycleStage == "Observation");
        Assert.Contains(logs, l => l.AgentName == "MedicalEvaluationAgent" && l.CycleStage == "Analysis");
        Assert.Contains(logs, l => l.AgentName == "MedicalEvaluationAgent" && l.CycleStage == "Decision");
    }
}
