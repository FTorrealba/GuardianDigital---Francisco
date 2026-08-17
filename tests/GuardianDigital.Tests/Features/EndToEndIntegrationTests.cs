using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Application.Features.Dashboard;
using GuardianDigital.Application.Features.Onboarding;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using GuardianDigital.Domain.ValueObjects;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuardianDigital.Tests.Features;

/// <summary>
/// Point 11: End-to-End Integration Test Suite running against an in-memory SQLite relational database.
/// Tests the full multi-agent lifecycles across Onboarding, Anomaly Detection, LLM Interpretation,
/// Medical Risk Evaluation, Emergency Action Dispatch, and Learning Memory.
/// </summary>
public class EndToEndIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<GuardianDbContext> _contextOptions;

    public EndToEndIntegrationTests()
    {
        // Set up isolated in-memory SQLite connection
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<GuardianDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    private GuardianDbContext CreateDbContext() => new(_contextOptions);

    public void Dispose()
    {
        _connection.Dispose();
    }

    // =========================================================================
    // FLOW 1: User Signup with Fewer than 3 Contacts -> Rejected
    // =========================================================================
    [Fact]
    public async Task Flow1_UserSignup_WithFewerThan3Contacts_IsRejectedWithBadRequest()
    {
        // Arrange
        using var db = CreateDbContext();

        var requestWithTwoContacts = new CreateUser.CreateUserRequest(
            FullName: "Carlos Gomez",
            NationalId: "20887766",
            DateOfBirth: new DateTime(1950, 5, 20),
            Gender: "Male",
            PrimaryPhone: "+54911554433",
            Address: "Avenida Santa Fe 2500",
            HealthInsurance: "Swiss Medical",
            BloodType: "O+",
            MedicalProfile: new CreateUser.MedicalProfileRequest(
                MedicalHistory: "Type 2 Diabetes",
                CurrentMedication: new List<string> { "Metformin" },
                KnownAllergies: new List<string> { "Aspirin" },
                PreexistingConditions: new List<string> { "Diabetes" }
            ),
            EmergencyContacts: new List<CreateUser.EmergencyContactRequest>
            {
                new("Contact 1", "Son", "+54911111111", "Call"),
                new("Contact 2", "Daughter", "+54911222222", "SMS")
                // Only 2 contacts provided (minimum required is 3)
            }
        );

        // Act
        var result = await CreateUser.HandleAsync(requestWithTwoContacts, db);

        // Assert
        var badRequest = Assert.IsAssignableFrom<IResult>(result);
        Assert.NotNull(badRequest);

        // Verify zero users were saved in the database
        var totalUsers = await db.Users.CountAsync();
        Assert.Equal(0, totalUsers);
    }

    // =========================================================================
    // FLOW 2: Fall Simulation -> Incident -> PossibleEmergency -> Emergency Action
    // =========================================================================
    [Fact]
    public async Task Flow2_FallSimulation_To_EmergencyServicesDispatch_CompleteFlow()
    {
        using var db = CreateDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);
        var riskEvaluator = new RiskEvaluationService(db, agentLogger, NullLogger<RiskEvaluationService>.Instance);
        var emergencyService = new EmergencyManagementService(db, agentLogger, NullLogger<EmergencyManagementService>.Instance);

        // 1. Onboarding User with 3 Contacts and Medical Profile
        var user = new User(
            "Elena Vasquez",
            "30998877A",
            new DateTime(1944, 4, 12), // 80+ years old
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
            CurrentMedication = new List<string> { "Amlodipine 10mg", "Calcium D3" },
            KnownAllergies = new List<string> { "Penicillin", "Sulfa" },
            PreexistingConditions = new List<string> { "Hypertension", "Osteoporosis" }
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 2. Hardware Sensor Seeding & Fall Anomaly Simulation (5.2G Vector + Immobility)
        var motionSensor = new LinkedDevice
        {
            UserId = user.Id,
            Type = DeviceType.MotionSensor,
            Status = DeviceStatus.Active
        };
        db.LinkedDevices.Add(motionSensor);
        await db.SaveChangesAsync();

        var fallReading = new SensorReading
        {
            DeviceId = motionSensor.Id,
            DataType = DataType.Motion,
            Value = "CRITICAL: SUDDEN FALL IMPACT DETECTED (5.2G Vector) - PATIENT IMMOBILE",
            Timestamp = DateTime.UtcNow
        };
        db.SensorReadings.Add(fallReading);

        // 3. Autonomous Incident Creation (Detected)
        var incident = new Incident
        {
            UserId = user.Id,
            Origin = IncidentOrigin.Sensor,
            OriginalDescription = "FALL DETECTED: Sudden 5.2G impact vector on MotionSensor followed by prolonged immobility",
            RiskLevel = RiskLevel.Mild, // Initial baseline before medical evaluation
            Status = IncidentStatus.Detected,
            Timestamp = DateTime.UtcNow
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        // 4. Point 6: Preliminary Medical Evaluation Agent
        var evalResult = await EvaluateMedicalRisk.HandleAsync(incident.Id, riskEvaluator);
        var evalOk = Assert.IsType<Ok<EvaluateMedicalRisk.EvaluateMedicalRiskResponse>>(evalResult);
        Assert.NotNull(evalOk.Value);
        Assert.Equal("PossibleEmergency", evalOk.Value.FinalRiskLevel);
        Assert.Equal("UnderEvaluation", evalOk.Value.Status);
        Assert.True(evalOk.Value.HardRuleTriggered);

        // Verify DB State after Evaluation
        var underEvalIncident = await db.Incidents.FindAsync(incident.Id);
        Assert.NotNull(underEvalIncident);
        Assert.Equal(RiskLevel.PossibleEmergency, underEvalIncident.RiskLevel);
        Assert.Equal(IncidentStatus.UnderEvaluation, underEvalIncident.Status);

        // 5. Point 7: Emergency Management Action Dispatch
        var dispatchResult = await emergencyService.DispatchActionsAsync(incident.Id);
        Assert.NotNull(dispatchResult);
        Assert.Equal("ActionTaken", dispatchResult.Status);
        Assert.True(dispatchResult.EmergencyProtocolActivated);
        Assert.Equal(30, dispatchResult.DispatchCountdownSeconds);
        Assert.Contains(dispatchResult.ActionsDispatched, a => a.ActionType == "ContactEmergencyServices");
        Assert.Contains(dispatchResult.ActionsDispatched, a => a.ActionType == "NotifyFamily");

        // Verify Database Incident State
        var finalizedIncident = await db.Incidents.Include(i => i.ActionsExecuted).FirstOrDefaultAsync(i => i.Id == incident.Id);
        Assert.NotNull(finalizedIncident);
        Assert.Equal(IncidentStatus.ActionTaken, finalizedIncident.Status);
        Assert.Equal(2, finalizedIncident.ActionsExecuted.Count);

        // 6. Point 7: Public Read-Only Rescue Sheet Access for Paramedics
        var rescueResult = await GetRescueSheet.HandleAsync(incident.Id, emergencyService);
        var rescueOk = Assert.IsType<Ok<RescueSheetDto>>(rescueResult);
        Assert.NotNull(rescueOk.Value);
        Assert.Equal("Elena Vasquez", rescueOk.Value.PatientFullName);
        Assert.Equal("AB+", rescueOk.Value.BloodType);
        Assert.Contains("Penicillin", rescueOk.Value.KnownAllergies);
        Assert.Contains("Hypertension", rescueOk.Value.PreexistingConditions);
        Assert.Equal(3, rescueOk.Value.EmergencyContacts.Count);
    }

    // =========================================================================
    // FLOW 3: Mild Symptom Reported -> Classified as Mild -> Recommendation & Appointment
    // =========================================================================
    [Fact]
    public async Task Flow3_MildSymptomReported_To_GeneralRecommendationAndAppointment_CompleteFlow()
    {
        using var db = CreateDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);
        var configuration = new ConfigurationBuilder().Build();
        var llmService = new LanguageModelService(NullLogger<LanguageModelService>.Instance, configuration);
        var riskEvaluator = new RiskEvaluationService(db, agentLogger, NullLogger<RiskEvaluationService>.Instance);
        var emergencyService = new EmergencyManagementService(db, agentLogger, NullLogger<EmergencyManagementService>.Instance);

        // 1. Seed Patient
        var user = new User(
            "Mario Rossi",
            "18223344A",
            new DateTime(1965, 8, 10),
            "Male",
            "+54911223344",
            "Avenida Cordoba 800",
            new BloodType("O+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "Lucia Rossi", Relationship = "Wife", Phone = "+54911556677", PreferredMethod = ContactPreferredMethod.Call },
                new() { ContactName = "Franco Rossi", Relationship = "Son", Phone = "+54911667788", PreferredMethod = ContactPreferredMethod.SMS },
                new() { ContactName = "Clinica Central", Relationship = "Clinic", Phone = "+54911778899", PreferredMethod = ContactPreferredMethod.PushNotification }
            },
            "Medife Bronce"
        );
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // 2. Point 5: Report Mild Symptom via Text
        var symptomRequest = new ReportSymptom.ReportSymptomRequest(
            Message: "Tengo un leve dolor de cabeza y cansancio después de trabajar",
            Origin: "Text",
            UserId: user.Id
        );

        var reportResult = await ReportSymptom.HandleAsync(symptomRequest, db, llmService, agentLogger);
        var reportOk = Assert.IsType<Ok<ReportSymptom.ReportSymptomResponse>>(reportResult);
        Assert.NotNull(reportOk.Value);
        Assert.Equal("mild", reportOk.Value.SuggestedUrgencyLevel);
        Assert.NotEmpty(reportOk.Value.SuggestedQuestions);

        var incidentId = reportOk.Value.IncidentId;

        // 3. Point 6: Medical Risk Evaluation Agent
        var evalResult = await EvaluateMedicalRisk.HandleAsync(incidentId, riskEvaluator);
        var evalOk = Assert.IsType<Ok<EvaluateMedicalRisk.EvaluateMedicalRiskResponse>>(evalResult);
        Assert.NotNull(evalOk.Value);
        Assert.Equal("Mild", evalOk.Value.FinalRiskLevel);
        Assert.Equal("UnderEvaluation", evalOk.Value.Status);

        // 4. Point 7: Action Dispatch for Mild Incident
        var dispatchResult = await emergencyService.DispatchActionsAsync(incidentId);
        Assert.NotNull(dispatchResult);
        Assert.False(dispatchResult.EmergencyProtocolActivated);
        Assert.Single(dispatchResult.ActionsDispatched);
        Assert.Equal("GeneralRecommendation", dispatchResult.ActionsDispatched[0].ActionType);

        // 5. Point 7: Request Non-Urgent Medical Appointment
        var appointmentRequest = new RequestAppointment.RequestAppointmentRequest(Notes: "Chequeo por cefalea tensional");
        var apptResult = await RequestAppointment.HandleAsync(incidentId, appointmentRequest, emergencyService);
        var apptOk = Assert.IsType<Ok<DispatchedActionSummary>>(apptResult);
        Assert.NotNull(apptOk.Value);
        Assert.Equal("RequestMedicalAppointment", apptOk.Value.ActionType);

        // Verify Database Records
        var finalIncident = await db.Incidents.Include(i => i.ActionsExecuted).FirstOrDefaultAsync(i => i.Id == incidentId);
        Assert.NotNull(finalIncident);
        Assert.Equal(IncidentStatus.ActionTaken, finalIncident.Status);
        Assert.Equal(2, finalIncident.ActionsExecuted.Count); // GeneralRecommendation + RequestMedicalAppointment
    }

    // =========================================================================
    // FLOW 4: Incident Marked as False Alarm -> Reflected in Learning Stats
    // =========================================================================
    [Fact]
    public async Task Flow4_IncidentMarkedAsFalseAlarm_IsReflectedInLearningStats_CompleteFlow()
    {
        using var db = CreateDbContext();
        var agentLogger = new AgentLogService(NullLogger<AgentLogService>.Instance);
        var learningStatsService = new LearningStatsService(db, agentLogger, NullLogger<LearningStatsService>.Instance);

        // Seed Patient to satisfy foreign key constraint
        var user = new User(
            "Mateo Fernandez",
            "33445566C",
            new DateTime(1975, 3, 14),
            "Male",
            "+54911445566",
            "Calle Corrientes 1500",
            new BloodType("A+"),
            new List<EmergencyContact>
            {
                new() { ContactName = "Lucia Fernandez", Relationship = "Sister", Phone = "+54911556677" },
                new() { ContactName = "Carlos Fernandez", Relationship = "Brother", Phone = "+54911667788" },
                new() { ContactName = "Mariana Fernandez", Relationship = "Mother", Phone = "+54911778899" }
            }
        );
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userId = user.Id;

        // 1. Seed 3 Normal Incidents
        for (int i = 0; i < 3; i++)
        {
            db.Incidents.Add(new Incident
            {
                UserId = userId,
                Origin = IncidentOrigin.Sensor,
                OriginalDescription = $"Sensor reading anomaly #{i}",
                RiskLevel = RiskLevel.Urgent,
                Status = IncidentStatus.Closed,
                Timestamp = DateTime.UtcNow.AddDays(-i)
            });
        }

        // 2. Create Active Incident
        var incident = new Incident
        {
            UserId = userId,
            Origin = IncidentOrigin.Sensor,
            OriginalDescription = "FALL DETECTED on motion sensor",
            RiskLevel = RiskLevel.PossibleEmergency,
            Status = IncidentStatus.Detected,
            Timestamp = DateTime.UtcNow
        };
        db.Incidents.Add(incident);
        await db.SaveChangesAsync();

        // 3. Mark Incident as False Alarm with Feedback
        var falseAlarmRequest = new MarkFalseAlarm.MarkFalseAlarmRequest("Sensor dropped on carpet while vacuuming");
        var markResult = await MarkFalseAlarm.HandleAsync(incident.Id, falseAlarmRequest, db, agentLogger);

        var markOk = Assert.IsAssignableFrom<IResult>(markResult);
        Assert.NotNull(markOk);

        // Verify Incident Status
        var updatedIncident = await db.Incidents.FindAsync(incident.Id);
        Assert.NotNull(updatedIncident);
        Assert.Equal(IncidentStatus.FalseAlarm, updatedIncident.Status);
        Assert.Contains("Sensor dropped on carpet", updatedIncident.OriginalDescription);

        // 4. Point 8: Verify 30-Day Learning Statistics
        var stats = await learningStatsService.GetLearningStatsAsync(userId);
        Assert.NotNull(stats);
        Assert.Equal(4, stats.TotalIncidentsLast30Days); // 3 closed + 1 false alarm
        Assert.Equal(1, stats.FalseAlarmsLast30Days);
        Assert.Equal(25.0, stats.FalseAlarmPercentage); // 1 out of 4 = 25%
        Assert.Equal(75.0, stats.PrecisionScore); // 100 - 25 = 75%
        Assert.Equal(1, stats.IncidentsByStatus["FalseAlarm"]);
    }
}
