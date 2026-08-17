using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.ValueObjects;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuardianDigital.Tests.Features;

public class ReportSymptomTests
{
    private GuardianDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseInMemoryDatabase(databaseName: "ReportSymptomTestDb_" + Guid.NewGuid())
            .Options;

        return new GuardianDbContext(options);
    }

    private LanguageModelService CreateLlmService()
    {
        var config = new ConfigurationBuilder().Build();
        return new LanguageModelService(NullLogger<LanguageModelService>.Instance, config);
    }

    [Fact]
    public async Task LlmService_ChestPainAndBreathing_ReturnsPossibleEmergencyWithFollowUpQuestions()
    {
        // Arrange
        var llm = CreateLlmService();
        var message = "I'm having trouble breathing and my chest hurts";

        // Act
        var result = await llm.InterpretSymptomAsync(message);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("possible_emergency", result.SuggestedUrgencyLevel);
        Assert.Contains(result.DetectedSymptoms, s => s.Contains("chest", StringComparison.OrdinalIgnoreCase) || s.Contains("discomfort", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(result.SuggestedQuestions);
        Assert.InRange(result.SuggestedQuestions.Count, 1, 3);
        Assert.NotEmpty(result.ConversationalResponse);
    }

    [Fact]
    public async Task LlmService_HighFeverAndChills_ReturnsUrgent()
    {
        // Arrange
        var llm = CreateLlmService();
        var message = "Tengo fiebre alta de 39 grados y escalofríos";

        // Act
        var result = await llm.InterpretSymptomAsync(message);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("urgent", result.SuggestedUrgencyLevel);
        Assert.NotEmpty(result.SuggestedQuestions);
    }

    [Fact]
    public async Task LlmService_MildTensionHeadache_ReturnsMild()
    {
        // Arrange
        var llm = CreateLlmService();
        var message = "Tengo un dolor de cabeza leve por estar frente a la pantalla";

        // Act
        var result = await llm.InterpretSymptomAsync(message);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("mild", result.SuggestedUrgencyLevel);
        Assert.NotEmpty(result.SuggestedQuestions);
    }

    [Fact]
    public async Task ReportSymptom_CreatesIncidentInDb_WithVoiceOriginAndDetectedStatus()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var llm = CreateLlmService();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);

        var user = new User(
            "Maria Gomez",
            "33445566A",
            new DateTime(1960, 4, 15),
            "Female",
            "+54911223344",
            "Calle Falsa 123",
            new BloodType("O+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "Contact 1", Relationship = "Son", Phone = "+111111" },
                new() { ContactName = "Contact 2", Relationship = "Daughter", Phone = "+222222" },
                new() { ContactName = "Contact 3", Relationship = "Neighbor", Phone = "+333333" }
            }
        );
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var request = new ReportSymptom.ReportSymptomRequest(
            Message: "I feel sudden numbness on the left side of my face and my arm is weak",
            Origin: "Voice",
            UserId: user.Id
        );

        // Act
        var httpResult = await ReportSymptom.HandleAsync(request, db, llm, agentLogger);

        // Assert HTTP Result
        var okResult = Assert.IsType<Ok<ReportSymptom.ReportSymptomResponse>>(httpResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("possible_emergency", okResult.Value.SuggestedUrgencyLevel);
        Assert.Equal("Voice", okResult.Value.Origin);
        Assert.NotEmpty(okResult.Value.SuggestedQuestions);

        // Assert Database State
        var incident = await db.Incidents
            .Include(i => i.UserResponses)
            .FirstOrDefaultAsync(i => i.Id == okResult.Value.IncidentId);

        Assert.NotNull(incident);
        Assert.Equal(user.Id, incident.UserId);
        Assert.Equal(IncidentOrigin.Voice, incident.Origin);
        Assert.Equal(IncidentStatus.Detected, incident.Status);
        Assert.Equal(RiskLevel.PossibleEmergency, incident.RiskLevel);
        Assert.NotEmpty(incident.UserResponses);

        // Assert Agent Logs
        var logs = agentLogger.GetLogs();
        Assert.Contains(logs, l => l.AgentName == "InteractionAgent" && l.CycleStage == "Observation");
        Assert.Contains(logs, l => l.AgentName == "InteractionAgent" && l.CycleStage == "Analysis");
        Assert.Contains(logs, l => l.AgentName == "InteractionAgent" && l.CycleStage == "Decision");
    }

    [Fact]
    public async Task ReportSymptom_WithTextOrigin_CreatesTextIncident()
    {
        // Arrange
        using var db = CreateInMemoryDbContext();
        var llm = CreateLlmService();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);

        var request = new ReportSymptom.ReportSymptomRequest(
            Message: "Tengo un dolor muscular leve en las piernas por caminar",
            Origin: "Text"
        );

        // Act
        var httpResult = await ReportSymptom.HandleAsync(request, db, llm, agentLogger);

        // Assert
        var okResult = Assert.IsType<Ok<ReportSymptom.ReportSymptomResponse>>(httpResult);
        Assert.NotNull(okResult.Value);
        Assert.Equal("mild", okResult.Value.SuggestedUrgencyLevel);
        Assert.Equal("Text", okResult.Value.Origin);

        var incident = await db.Incidents.FirstOrDefaultAsync(i => i.Id == okResult.Value.IncidentId);
        Assert.NotNull(incident);
        Assert.Equal(IncidentOrigin.Text, incident.Origin);
        Assert.Equal(RiskLevel.Mild, incident.RiskLevel);
    }
}
