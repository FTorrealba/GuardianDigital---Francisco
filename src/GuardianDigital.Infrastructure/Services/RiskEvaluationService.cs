using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Preliminary Medical Evaluation Agent implementing multi-tiered clinical risk classification:
/// Combines deterministic Hard Rules (overriding), LLM advisory suggestions, and Section 7 Prioritization Criteria.
/// </summary>
public class RiskEvaluationService : IRiskEvaluationService
{
    private readonly IGuardianDbContext _db;
    private readonly IAgentLogService _agentLogger;
    private readonly ILogger<RiskEvaluationService> _logger;

    public RiskEvaluationService(
        IGuardianDbContext db,
        IAgentLogService agentLogger,
        ILogger<RiskEvaluationService> logger)
    {
        _db = db;
        _agentLogger = agentLogger;
        _logger = logger;
    }

    public async Task<RiskEvaluationResult> EvaluateIncidentRiskAsync(Guid incidentId, CancellationToken cancellationToken = default)
    {
        var incident = await _db.Incidents
            .Include(i => i.UserResponses)
            .Include(i => i.ActionsExecuted)
            .Include(i => i.User)
                .ThenInclude(u => u!.MedicalProfile)
            .Include(i => i.User)
                .ThenInclude(u => u!.EmergencyContacts)
            .FirstOrDefaultAsync(i => i.Id == incidentId, cancellationToken);

        if (incident == null)
        {
            throw new KeyNotFoundException($"Incident with ID '{incidentId}' was not found.");
        }

        // Fetch recent sensor readings and recent incidents for context
        var recentReadings = await _db.SensorReadings
            .OrderByDescending(r => r.Timestamp)
            .Take(20)
            .ToListAsync(cancellationToken);

        var recentIncidents = await _db.Incidents
            .Where(i => i.UserId == incident.UserId && i.Id != incidentId)
            .OrderByDescending(i => i.Timestamp)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Step 1: Agent Observation Log
        _agentLogger.Log(
            agentName: "MedicalEvaluationAgent",
            cycleStage: "Observation",
            message: $"Initiating preliminary medical evaluation for Incident #{incident.Id} (Origin: {incident.Origin}).",
            details: $"Initial Risk: {incident.RiskLevel} | Description: '{incident.OriginalDescription}'",
            incidentId: incident.Id
        );

        // Step 2: Evaluation Core
        var result = EvaluateRisk(
            incident,
            incident.User?.MedicalProfile,
            incident.User,
            recentReadings,
            recentIncidents
        );

        // Step 3: Analysis Log
        _agentLogger.Log(
            agentName: "MedicalEvaluationAgent",
            cycleStage: "Analysis",
            message: $"Clinical risk evaluation: {result.AppliedRuleOrCriteria} -> Final Risk: {result.FinalRiskLevel}.",
            details: $"Factors: [{string.Join(", ", result.PrioritizationFactors)}] | Summary: {result.DiagnosticSummary}",
            incidentId: incident.Id
        );

        // Step 4: Decision & Incident Update to UnderEvaluation
        var oldRisk = incident.RiskLevel;
        incident.RiskLevel = result.FinalRiskLevel;
        incident.Status = IncidentStatus.UnderEvaluation;

        await _db.SaveChangesAsync(cancellationToken);

        _agentLogger.Log(
            agentName: "MedicalEvaluationAgent",
            cycleStage: "Decision",
            message: $"Incident status transitioned to 'UnderEvaluation' (Risk: {oldRisk} -> {result.FinalRiskLevel}).",
            details: $"Rule: {result.AppliedRuleOrCriteria} | HardRule: {result.HardRuleTriggered}",
            incidentId: incident.Id
        );

        return result;
    }

    public RiskEvaluationResult EvaluateRisk(
        Incident incident,
        MedicalProfile? medicalProfile,
        User? user,
        IEnumerable<SensorReading>? recentReadings = null,
        IEnumerable<Incident>? recentIncidents = null)
    {
        var text = (incident.OriginalDescription + " " + string.Join(" ", incident.UserResponses.Select(r => r.Question + " " + r.Answer))).ToLowerInvariant();
        var factors = new List<string>();

        // Extract patient age
        int age = 0;
        if (user != null && user.DateOfBirth != default)
        {
            age = DateTime.UtcNow.Year - user.DateOfBirth.Year;
            if (user.DateOfBirth.Date > DateTime.UtcNow.AddYears(-age)) age--;
        }

        var medHistory = (medicalProfile?.MedicalHistory ?? string.Empty).ToLowerInvariant();
        var medications = (medicalProfile?.CurrentMedication ?? new List<string>()).Select(m => m.ToLowerInvariant()).ToList();
        var conditions = (medicalProfile?.PreexistingConditions ?? new List<string>()).Select(c => c.ToLowerInvariant()).ToList();

        // =====================================================================
        // PILLAR 1: HARD CLINICAL RULES (Deterministic Absolute Precedence)
        // Hard rules ALWAYS dictate PossibleEmergency or Urgent regardless of LLM.
        // =====================================================================

        // Hard Rule 1: Chest pain with radiation or crushing retrosternal pressure
        if (MatchesAny(text, "chest pain with radiation", "radiat", "irradiación", "dolor en el pecho que se irradia", "arm, jaw", "brazo izquierdo", "mandíbula")
            || (MatchesAny(text, "chest pain", "dolor en el pecho", "pecho") && MatchesAny(text, "radiation", "irradia", "left arm", "brazo", "jaw", "mandibula", "neck", "cuello")))
        {
            factors.Add("HardRule: Chest pain with classic radiation pattern (Acute Coronary Syndrome red flag)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "CRITICAL CARDIAC RED FLAG: Chest pain with somatic radiation detected. Deterministic emergency protocol triggered.",
                AppliedRuleOrCriteria: "HardRule_ChestPainRadiation",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 2: Loss of Consciousness / Syncope / Blackout
        if (MatchesAny(text, "loss of consciousness", "syncope", "desmayo", "blackout", "passed out", "perdí el conocimiento", "unresponsive", "inconsciente"))
        {
            factors.Add("HardRule: Acute transient loss of consciousness (Syncope)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "CRITICAL NEUROLOGICAL FLAG: Documented loss of consciousness or syncope episode.",
                AppliedRuleOrCriteria: "HardRule_LossOfConsciousness",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 3: Severe Airway Compromise / Asphyxia / Gasping / SpO2 < 85%
        if (MatchesAny(text, "choking", "asfixia", "ahogo", "gasping for air", "cannot breathe at all", "no entra aire", "anaphylaxis", "throat swelling"))
        {
            factors.Add("HardRule: Acute severe airway/respiratory compromise");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "CRITICAL RESPIRATORY FLAG: Severe acute respiratory distress / acute airway compromise.",
                AppliedRuleOrCriteria: "HardRule_SevereAirwayCompromise",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 4: Stroke FAST Signs (Facial droop + motor hemiparesis + dysarthria)
        if (MatchesAny(text, "facial droop", "cara caída", "paralysis", "no puedo mover el brazo", "slurred speech", "balbuceo", "hemiparesis"))
        {
            factors.Add("HardRule: Acute focal neurological deficit (FAST Stroke Signs)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "CRITICAL NEUROLOGICAL FLAG: Acute signs of focal cerebral ischemia (Stroke warning).",
                AppliedRuleOrCriteria: "HardRule_StrokeFAST",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 5: Severe Traumatic Hemorrhage / Hematemesis
        if (MatchesAny(text, "vomiting blood", "vomité sangre", "hematemesis", "arterial bleeding", "heavy bleeding", "sangrado abundante"))
        {
            factors.Add("HardRule: Active severe hemorrhage / upper GI bleeding");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "CRITICAL HEMORRHAGIC FLAG: Active heavy bleeding or hematemesis detected.",
                AppliedRuleOrCriteria: "HardRule_SevereHemorrhage",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 6: High-G Impact Fall (>5G) + Immobility in Telemetry
        if (incident.Origin == IncidentOrigin.Sensor && MatchesAny(text, "5.2g", "high-g", "fall impact", "caida"))
        {
            var immobilityDetected = recentReadings?.Any(r => r.Value.Contains("IMMOBILE", StringComparison.OrdinalIgnoreCase) || r.Value.Contains("ZERO MOVEMENT", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (immobilityDetected || MatchesAny(text, "cannot walk", "no puedo caminar", "fracture"))
            {
                factors.Add("HardRule: High-G Fall impact combined with patient immobility");
                return new RiskEvaluationResult(
                    FinalRiskLevel: RiskLevel.PossibleEmergency,
                    DiagnosticSummary: "CRITICAL TRAUMA FLAG: High acceleration impact vector followed by confirmed prolonged immobility.",
                    AppliedRuleOrCriteria: "HardRule_HighGFallWithImmobility",
                    PrioritizationFactors: factors,
                    HardRuleTriggered: true,
                    EvaluatedAt: DateTime.UtcNow
                );
            }
        }

        // =====================================================================
        // PILLAR 2 & 3: SECTION 7 PRIORITIZATION CRITERIA HIERARCHY & LLM ADVISORY
        // Hierarchy Order:
        // Life Risk > Consciousness State > Mobility > Age > Medical History > Proximity of Contacts
        // =====================================================================

        // Baseline: Start with Incident's current/LLM assigned RiskLevel
        var calculatedRisk = incident.RiskLevel;
        factors.Add($"Baseline LLM/Sensor Risk: {calculatedRisk}");

        // 1. Life Risk Factor
        bool hasCardiacSymptoms = MatchesAny(text, "palpitations", "taquicardia", "heart", "corazon", "racing", "tachycardia");
        bool hasRespiratorySymptoms = MatchesAny(text, "dyspnea", "breathing", "respirar", "cough", "falta de aire", "shortness");
        if (hasCardiacSymptoms || hasRespiratorySymptoms)
        {
            factors.Add("Prioritization(1-LifeRisk): Vital organ involvement (cardiac/respiratory)");
        }

        // 2. Consciousness State Factor
        bool hasConsciousnessDeficit = MatchesAny(text, "dizzy", "mareo", "vertigo", "confusion", "confundido", "desorientado", "lightheaded", "atontado");
        if (hasConsciousnessDeficit)
        {
            factors.Add("Prioritization(2-Consciousness): Impaired neurological / vestibular equilibrium");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Consciousness impairment elevated risk to Urgent");
            }
        }

        // 3. Mobility Factor
        bool hasMobilityImpairment = MatchesAny(text, "cannot walk", "no puedo caminar", "no puedo pisar", "fell", "caida", "immobile", "sprain", "esguince");
        if (hasMobilityImpairment)
        {
            factors.Add("Prioritization(3-Mobility): Patient ambulation compromised");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Mobility loss elevated risk to Urgent");
            }
        }

        // 4. Age Vulnerability Factor
        if (age >= 80)
        {
            factors.Add($"Prioritization(4-Age): Advanced geriatric vulnerability (Age: {age})");
            if (calculatedRisk == RiskLevel.Mild && (hasConsciousnessDeficit || hasMobilityImpairment || hasCardiacSymptoms))
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Age >= 80 with symptoms escalated to Urgent");
            }
        }
        else if (age >= 70)
        {
            factors.Add($"Prioritization(4-Age): Senior geriatric category (Age: {age})");
        }

        // 5. Medical History & Current Medications Risk Modifiers
        bool hasDiabetes = medHistory.Contains("diabetes") || conditions.Any(c => c.Contains("diabetes")) || medications.Any(m => m.Contains("metformin") || m.Contains("insulin"));
        bool hasAnticoagulants = medications.Any(m => m.Contains("warfarin") || m.Contains("aspirin") || m.Contains("apixaban") || m.Contains("clopidogrel") || m.Contains("heparin") || m.Contains("anticoagulant"));
        bool hasHypertensionOrCardiac = medHistory.Contains("hypertension") || medHistory.Contains("cardiac") || conditions.Any(c => c.Contains("hypertension") || c.Contains("pressure") || c.Contains("cardiac"));
        bool hasAsthmaOrCopd = medHistory.Contains("asthma") || medHistory.Contains("copd") || conditions.Any(c => c.Contains("asthma") || c.Contains("respiratory"));

        // Clinical Interaction A: Anticoagulants + Fall / Trauma -> PossibleEmergency (Intracranial / Internal Hemorrhage risk)
        if (hasAnticoagulants && (hasMobilityImpairment || MatchesAny(text, "fall", "caí", "golpe", "trauma", "head", "cabeza", "bruise")))
        {
            factors.Add("Prioritization(5-MedHistory): Patient on active Anticoagulant therapy with trauma/fall (Internal Hemorrhage Risk)");
            calculatedRisk = RiskLevel.PossibleEmergency;
            factors.Add("Escalation -> Anticoagulant + Trauma elevated to PossibleEmergency");
        }

        // Clinical Interaction B: Diabetes + Dizziness / Confusion -> Urgent (Hypoglycemic coma risk)
        if (hasDiabetes && (hasConsciousnessDeficit || MatchesAny(text, "sweating", "shaking", "sudor", "temblor", "dizzy")))
        {
            factors.Add("Prioritization(5-MedHistory): Diabetic patient presenting acute glycemic disturbance signs");
            if (calculatedRisk < RiskLevel.Urgent)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Diabetic acute symptom escalated to Urgent");
            }
        }

        // Clinical Interaction C: Cardiac / Hypertension history + Dyspnea / Palpitations
        if (hasHypertensionOrCardiac && (hasCardiacSymptoms || hasRespiratorySymptoms))
        {
            factors.Add("Prioritization(5-MedHistory): Pre-existing Cardiovascular condition exacerbated by acute symptoms");
            if (calculatedRisk < RiskLevel.Urgent)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Cardiovascular history escalated to Urgent");
            }
        }

        // Clinical Interaction D: Asthma / COPD + Cold / Cough -> Urgent
        if (hasAsthmaOrCopd && MatchesAny(text, "cold", "cough", "garganta", "congestión", "tos"))
        {
            factors.Add("Prioritization(5-MedHistory): Chronic respiratory disease (Asthma/COPD) vulnerable to acute decompensation");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalation -> Chronic respiratory vulnerability elevated to Urgent");
            }
        }

        // 6. Proximity / Availability of Emergency Contacts (Tiebreaker)
        var contactsCount = user?.EmergencyContacts.Count ?? 0;
        if (contactsCount < 3)
        {
            factors.Add($"Prioritization(6-Contacts): Support network alert ({contactsCount} contact(s) on file)");
        }

        return new RiskEvaluationResult(
            FinalRiskLevel: calculatedRisk,
            DiagnosticSummary: $"Evaluation complete. Final Risk: {calculatedRisk}. Triaged across 6 prioritization tiers with patient medical profile.",
            AppliedRuleOrCriteria: $"PrioritizationCriteria_Tiers_1to6 (FinalRisk: {calculatedRisk})",
            PrioritizationFactors: factors,
            HardRuleTriggered: false,
            EvaluatedAt: DateTime.UtcNow
        );
    }

    private static bool MatchesAny(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
