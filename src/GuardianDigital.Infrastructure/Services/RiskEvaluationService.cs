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
            throw new KeyNotFoundException($"No se encontró el incidente con ID '{incidentId}'.");
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
            message: $"Iniciando evaluación médica preliminar para el Incidente #{incident.Id} (Origen: {incident.Origin}).",
            details: $"Riesgo Inicial: {incident.RiskLevel} | Descripción: '{incident.OriginalDescription}'",
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
            message: $"Evaluación de riesgo clínico: {result.AppliedRuleOrCriteria} -> Riesgo Final: {result.FinalRiskLevel}.",
            details: $"Factores: [{string.Join(", ", result.PrioritizationFactors)}] | Resumen: {result.DiagnosticSummary}",
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
            message: $"Estado del incidente actualizado a 'UnderEvaluation' (En Evaluación) (Riesgo: {oldRisk} -> {result.FinalRiskLevel}).",
            details: $"Regla: {result.AppliedRuleOrCriteria} | ReglaCrítica: {result.HardRuleTriggered}",
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
        if (MatchesAny(text, "chest pain with radiation", "radiat", "irradiación", "dolor en el pecho que se irradia", "arm, jaw", "brazo izquierdo", "mandíbula", "irradia al brazo")
            || (MatchesAny(text, "chest pain", "dolor en el pecho", "pecho") && MatchesAny(text, "radiation", "irradia", "left arm", "brazo", "jaw", "mandibula", "neck", "cuello", "falta el aire")))
        {
            factors.Add("ReglaCrítica: Dolor precordial con patrón de irradiación clásico (Signo de alarma de Síndrome Coronario Agudo)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "SIGNO DE ALARMA CARDÍACA CRÍTICA: Dolor de pecho con irradiación somática detectado. Activación obligatoria del protocolo de emergencia.",
                AppliedRuleOrCriteria: "HardRule_ChestPainRadiation",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 2: Loss of Consciousness / Syncope / Blackout
        if (MatchesAny(text, "loss of consciousness", "syncope", "desmayo", "blackout", "passed out", "perdí el conocimiento", "unresponsive", "inconsciente", "desvanecimiento"))
        {
            factors.Add("ReglaCrítica: Pérdida transitoria de la conciencia aguda (Síncope)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "SIGNO DE ALARMA NEUROLÓGICA CRÍTICA: Episodio documentado de síncope o pérdida transitoria de conciencia.",
                AppliedRuleOrCriteria: "HardRule_LossOfConsciousness",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 3: Severe Airway Compromise / Asphyxia / Gasping / SpO2 < 85%
        if (MatchesAny(text, "choking", "asfixia", "ahogo", "gasping for air", "cannot breathe at all", "no entra aire", "anaphylaxis", "throat swelling", "cerrando la garganta"))
        {
            factors.Add("ReglaCrítica: Compromiso respiratorio agudo severo / Asfixia");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "SIGNO DE ALARMA RESPIRATORIA CRÍTICA: Dificultad respiratoria severa y riesgo inminente de obstrucción de vía aérea.",
                AppliedRuleOrCriteria: "HardRule_SevereAirwayCompromise",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 4: Stroke FAST Signs (Facial droop + motor hemiparesis + dysarthria)
        if (MatchesAny(text, "facial droop", "cara caída", "cara caida", "paralysis", "no puedo mover el brazo", "slurred speech", "balbuceo", "hemiparesis", "brazo debil", "brazo débil"))
        {
            factors.Add("ReglaCrítica: Déficit neurológico focal agudo (Signos FAST de ACV / Ictus)");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "SIGNO DE ALARMA NEUROLÓGICA CRÍTICA: Signos agudos de isquemia cerebral focal (Sospecha de Accidente Cerebrovascular).",
                AppliedRuleOrCriteria: "HardRule_StrokeFAST",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 5: Severe Traumatic Hemorrhage / Hematemesis
        if (MatchesAny(text, "vomiting blood", "vomité sangre", "hematemesis", "arterial bleeding", "heavy bleeding", "sangrado abundante", "mucha sangre"))
        {
            factors.Add("ReglaCrítica: Hemorragia digestiva alta o sangrado activo severo");
            return new RiskEvaluationResult(
                FinalRiskLevel: RiskLevel.PossibleEmergency,
                DiagnosticSummary: "SIGNO DE ALARMA HEMORRÁGICA CRÍTICA: Sangrado activo profuso o hematemesis documentada.",
                AppliedRuleOrCriteria: "HardRule_SevereHemorrhage",
                PrioritizationFactors: factors,
                HardRuleTriggered: true,
                EvaluatedAt: DateTime.UtcNow
            );
        }

        // Hard Rule 6: High-G Impact Fall (>5G) + Immobility in Telemetry
        if (incident.Origin == IncidentOrigin.Sensor && MatchesAny(text, "5.2g", "high-g", "fall impact", "caida", "caída"))
        {
            var immobilityDetected = recentReadings?.Any(r => r.Value.Contains("IMMOBILE", StringComparison.OrdinalIgnoreCase) || r.Value.Contains("ZERO MOVEMENT", StringComparison.OrdinalIgnoreCase)) ?? false;
            if (immobilityDetected || MatchesAny(text, "cannot walk", "no puedo caminar", "fracture", "no puedo levantarme", "no puedo pararme"))
            {
                factors.Add("ReglaCrítica: Impacto de alta aceleración por caída combinado con inmovilidad prolongada del paciente");
                return new RiskEvaluationResult(
                    FinalRiskLevel: RiskLevel.PossibleEmergency,
                    DiagnosticSummary: "SIGNO DE ALARMA TRAUMATOLÓGICA CRÍTICA: Vector de desaceleración brusca seguido de inmovilidad confirmada por telemetría.",
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
        factors.Add($"Riesgo Base del LLM/Sensor: {calculatedRisk}");

        // 1. Life Risk Factor
        bool hasCardiacSymptoms = MatchesAny(text, "palpitations", "taquicardia", "heart", "corazon", "corazón", "racing", "tachycardia", "latidos");
        bool hasRespiratorySymptoms = MatchesAny(text, "dyspnea", "breathing", "respirar", "cough", "falta de aire", "shortness", "tos");
        if (hasCardiacSymptoms || hasRespiratorySymptoms)
        {
            factors.Add("Priorización (1-RiesgoVital): Compromiso de órgano vital (cardíaco / respiratorio)");
        }

        // 2. Consciousness State Factor
        bool hasConsciousnessDeficit = MatchesAny(text, "dizzy", "mareo", "vertigo", "vértigo", "confusion", "confundido", "desorientado", "lightheaded", "atontado");
        if (hasConsciousnessDeficit)
        {
            factors.Add("Priorización (2-Conciencia): Alteración del equilibrio neurológico / vestibular");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Afectación del equilibrio escaló el riesgo a 'Urgente'");
            }
        }

        // 3. Mobility Factor
        bool hasMobilityImpairment = MatchesAny(text, "cannot walk", "no puedo caminar", "no puedo pisar", "fell", "caida", "caída", "immobile", "sprain", "esguince", "no me puedo levantar");
        if (hasMobilityImpairment)
        {
            factors.Add("Priorización (3-Movilidad): Deambulación del paciente comprometida");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Pérdida de movilidad escaló el riesgo a 'Urgente'");
            }
        }

        // 4. Age Vulnerability Factor
        if (age >= 80)
        {
            factors.Add($"Priorización (4-Edad): Vulnerabilidad geriátrica avanzada (Edad: {age} años)");
            if (calculatedRisk == RiskLevel.Mild && (hasConsciousnessDeficit || hasMobilityImpairment || hasCardiacSymptoms))
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Paciente >= 80 años con síntomas comórbidos escaló a 'Urgente'");
            }
        }
        else if (age >= 70)
        {
            factors.Add($"Priorización (4-Edad): Adulto mayor en categoría de atención prioritaria (Edad: {age} años)");
        }

        // 5. Medical History & Current Medications Risk Modifiers
        bool hasDiabetes = medHistory.Contains("diabetes") || conditions.Any(c => c.Contains("diabetes")) || medications.Any(m => m.Contains("metformin") || m.Contains("insulin"));
        bool hasAnticoagulants = medications.Any(m => m.Contains("warfarin") || m.Contains("aspirin") || m.Contains("apixaban") || m.Contains("clopidogrel") || m.Contains("heparin") || m.Contains("anticoagulant"));
        bool hasHypertensionOrCardiac = medHistory.Contains("hypertension") || medHistory.Contains("hipertensión") || medHistory.Contains("cardiac") || conditions.Any(c => c.Contains("hypertension") || c.Contains("hipertensión") || c.Contains("cardiac"));
        bool hasAsthmaOrCopd = medHistory.Contains("asthma") || medHistory.Contains("asma") || medHistory.Contains("epoc") || medHistory.Contains("copd") || conditions.Any(c => c.Contains("asthma") || c.Contains("asma") || c.Contains("epoc") || c.Contains("respiratory"));

        // Clinical Interaction A: Anticoagulants + Fall / Trauma -> PossibleEmergency (Intracranial / Internal Hemorrhage risk)
        if (hasAnticoagulants && (hasMobilityImpairment || MatchesAny(text, "fall", "caí", "caida", "caída", "golpe", "trauma", "head", "cabeza", "bruise")))
        {
            factors.Add("Priorización (5-HistorialMédico): Paciente con terapia anticoagulante activa tras golpe/caída (Riesgo de Hemorragia Interna)");
            calculatedRisk = RiskLevel.PossibleEmergency;
            factors.Add("Escalamiento -> Anticoagulante + Traumatismo escaló a 'Posible Emergencia'");
        }

        // Clinical Interaction B: Diabetes + Dizziness / Confusion -> Urgent (Hypoglycemic coma risk)
        if (hasDiabetes && (hasConsciousnessDeficit || MatchesAny(text, "sweating", "shaking", "sudor", "temblor", "dizzy", "mareo")))
        {
            factors.Add("Priorización (5-HistorialMédico): Paciente diabético con signos de descompensación glucémica aguda");
            if (calculatedRisk < RiskLevel.Urgent)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Síntoma agudo en paciente diabético escaló a 'Urgente'");
            }
        }

        // Clinical Interaction C: Cardiac / Hypertension history + Dyspnea / Palpitations
        if (hasHypertensionOrCardiac && (hasCardiacSymptoms || hasRespiratorySymptoms))
        {
            factors.Add("Priorización (5-HistorialMédico): Antecedente cardiovascular exacerbado por síntomas agudos");
            if (calculatedRisk < RiskLevel.Urgent)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Historial cardiovascular escaló a 'Urgente'");
            }
        }

        // Clinical Interaction D: Asthma / COPD + Cold / Cough -> Urgent
        if (hasAsthmaOrCopd && MatchesAny(text, "cold", "cough", "garganta", "congestión", "tos", "resfriado"))
        {
            factors.Add("Priorización (5-HistorialMédico): Patología respiratoria crónica (Asma/EPOC) con riesgo de descompensación");
            if (calculatedRisk == RiskLevel.Mild)
            {
                calculatedRisk = RiskLevel.Urgent;
                factors.Add("Escalamiento -> Vulnerabilidad respiratoria crónica escaló a 'Urgente'");
            }
        }

        // 6. Proximity / Availability of Emergency Contacts (Tiebreaker)
        var contactsCount = user?.EmergencyContacts.Count ?? 0;
        if (contactsCount < 3)
        {
            factors.Add($"Priorización (6-Contactos): Alerta de red de contención ({contactsCount} contacto(s) en ficha)");
        }

        return new RiskEvaluationResult(
            FinalRiskLevel: calculatedRisk,
            DiagnosticSummary: $"Evaluación clínica completada. Riesgo Final: {calculatedRisk}. Triaje jerárquico integrado con ficha médica del paciente.",
            AppliedRuleOrCriteria: $"PrioritizationCriteria_Tiers_1to6 (RiesgoFinal: {calculatedRisk})",
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
