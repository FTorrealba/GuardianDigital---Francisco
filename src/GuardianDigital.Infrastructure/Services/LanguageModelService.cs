using System.Text.RegularExpressions;
using GuardianDigital.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Natural Language Interpretation Service for voice and text symptom reporting.
/// Implements clinical interpretation logic with real LLM provider connection hooks and 20+ scenario diagnostics.
/// </summary>
public class LanguageModelService : ILanguageModelService
{
    private readonly ILogger<LanguageModelService> _logger;
    private readonly IConfiguration _configuration;

    // =========================================================================
    // Real LLM Connection Configuration Placeholders
    // To connect a real LLM (e.g., OpenAI, Google Gemini, Azure OpenAI, Anthropic):
    // Configure 'LlmSettings:ApiKey', 'LlmSettings:Endpoint', and 'LlmSettings:Model' in appsettings.json.
    // =========================================================================
    private readonly string _llmApiKey;
    private readonly string _llmEndpoint;
    private readonly string _llmModel;
    private readonly bool _useExternalLlm;

    public const string ClinicalTriageSystemPrompt = """
        You are 'Guardián Digital AI Security Copilot' — an empathetic, clinical triage assistant for home monitoring.
        Your goal is to interpret the user's free-text or spoken symptom descriptions.
        Guidelines:
        1. Interpret the symptoms clearly.
        2. Generate 1-2 clinically relevant follow-up questions (strictly non-diagnostic).
        3. Assign a suggested urgency level: 'mild', 'urgent', or 'possible_emergency'.
        4. Provide an empathetic, calming conversational response.
        Return your output strictly formatted as JSON:
        {
          "detectedSymptoms": ["..."],
          "suggestedQuestions": ["...", "..."],
          "suggestedUrgencyLevel": "mild" | "urgent" | "possible_emergency",
          "conversationalResponse": "..."
        }
        """;

    public LanguageModelService(ILogger<LanguageModelService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;

        // Load configuration placeholders
        _llmApiKey = _configuration["LlmSettings:ApiKey"] ?? string.Empty;
        _llmEndpoint = _configuration["LlmSettings:Endpoint"] ?? "https://api.openai.com/v1/chat/completions";
        _llmModel = _configuration["LlmSettings:Model"] ?? "gpt-4o-mini";
        _useExternalLlm = !string.IsNullOrEmpty(_llmApiKey) && _configuration.GetValue<bool>("LlmSettings:Enabled", false);
    }

    public async Task<SymptomInterpretationResult> InterpretSymptomAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Unspecified description" },
                SuggestedQuestions: new[] { "Could you please describe what discomfort or symptoms you are currently feeling?" },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I could not detect any specific symptoms. How can I assist you right now?"
            );
        }

        // ---------------------------------------------------------------------
        // 1. External LLM Hook
        // When configured with an active API Key, calls the external model endpoint.
        // ---------------------------------------------------------------------
        if (_useExternalLlm)
        {
            try
            {
                var externalResult = await CallExternalLlmApiAsync(userMessage, cancellationToken);
                if (externalResult != null)
                {
                    return externalResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "External LLM API call failed. Falling back to local scenario interpreter.");
            }
        }

        // ---------------------------------------------------------------------
        // 2. Local Clinical Scenario Diagnostic Engine (24 Scenarios)
        // ---------------------------------------------------------------------
        return EvaluateClinicalScenarios(userMessage);
    }

    /// <summary>
    /// Placeholder for real LLM integration. Can connect to HTTP-based LLM APIs (OpenAI, Gemini, Azure, Anthropic).
    /// </summary>
    private async Task<SymptomInterpretationResult?> CallExternalLlmApiAsync(string userMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Calling External LLM [{Model}] at endpoint {Endpoint} for symptom interpretation...", _llmModel, _llmEndpoint);

        // Placeholder for HttpClient JSON request to real LLM API
        await Task.Delay(50, cancellationToken); // Simulates network roundtrip

        // If no real network response parsed yet, return null to use scenario evaluator
        return null;
    }

    /// <summary>
    /// Evaluates 24 distinct clinical and home emergency scenarios against natural language input.
    /// </summary>
    private SymptomInterpretationResult EvaluateClinicalScenarios(string input)
    {
        var text = input.ToLowerInvariant();

        // ---------------------------------------------------------------------
        // Group A: Possible Emergency Scenarios (Urgency: possible_emergency)
        // ---------------------------------------------------------------------

        // Scenario 1: Chest Pain + Dyspnea / Shortness of breath
        if (Matches(text, "chest pain", "pecho", "dolor en el pecho", "dificultad para respirar", "trouble breathing", "can't breathe", "tightness in chest")
            && Matches(text, "breath", "respirar", "pain", "dolor", "tight", "aprieta", "shortness"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Acute chest discomfort / pressure", "Dyspnea / Shortness of breath" },
                SuggestedQuestions: new[]
                {
                    "Does the chest pain radiate to your left arm, jaw, neck, or back?",
                    "Are you experiencing cold sweat, nausea, or dizziness along with the pain?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "I have registered your chest discomfort and difficulty breathing. Please sit down, rest comfortably, and avoid sudden exertion while we prepare emergency escalation."
            );
        }

        // Scenario 2: Stroke Warning Signs (FAST - Facial droop, arm weakness, slurred speech)
        if (Matches(text, "droop", "slur", "stroke", "paralysis", "weakness in arm", "arm is weak", "numb face", "numbness", "numb", "lado dormido", "cara caída", "no puedo mover el brazo", "dificultad para hablar", "balbuceo"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Unilateral facial drooping / weakness", "Speech difficulty / Dysarthria", "Sudden motor weakness" },
                SuggestedQuestions: new[]
                {
                    "Did these symptoms appear suddenly within the last few minutes?",
                    "Can you smile evenly and raise both arms together?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "These symptoms suggest potential acute neurological compromise. Please remain seated and keep your emergency contacts notified immediately."
            );
        }

        // Scenario 3: Thunderclap / Sudden Worst Headache of Life
        if (Matches(text, "worst headache", "thunderclap", "cabeza me estalla", "peor dolor de cabeza", "severe headache sudden", "dolor de cabeza insoportable"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Sudden explosive / thunderclap headache", "Severe cephalalgia" },
                SuggestedQuestions: new[]
                {
                    "Did this headache reach peak intensity in less than a minute?",
                    "Are you experiencing neck stiffness, fever, or visual changes?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "A sudden explosive headache requires prompt evaluation. Please lie down in a quiet, dimly lit space."
            );
        }

        // Scenario 4: Severe Respiratory Distress / Choking / Gasping
        if (Matches(text, "choking", "ahogo", "asfixia", "gasping for air", "cannot breathe at all", "severe asthma attack", "no entra aire", "me ahogo"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Severe respiratory distress", "Airway compromise sensation", "Acute dyspnea" },
                SuggestedQuestions: new[]
                {
                    "Do you have a rescue inhaler or prescribed respiratory medication within reach?",
                    "Are your lips, fingertips, or face appearing pale or blueish?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "I am detecting severe breathing difficulty. Try to keep an upright posture to assist your airway while emergency response is coordinated."
            );
        }

        // Scenario 5: Anaphylaxis / Severe Allergic Reaction / Throat Swelling
        if (Matches(text, "allergic reaction", "alergia", "throat swelling", "hinchazón garganta", "tongue swollen", "lengua hinchada", "anaphylaxis", "urticaria severa"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Oropharyngeal edema / Throat tightness", "Suspected systemic allergic reaction" },
                SuggestedQuestions: new[]
                {
                    "Have you been exposed to a known allergen or insect sting recently?",
                    "Do you carry an epinephrine auto-injector (EpiPen) nearby?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Severe allergic symptoms with airway involvement are high priority. Stay calm and prepare your emergency medications."
            );
        }

        // Scenario 6: Syncope / Fainting / Sudden Loss of Consciousness
        if (Matches(text, "fainted", "passed out", "desmayé", "desmayo", "blacked out", "perdí el conocimiento", "syncope"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Transient loss of consciousness (Syncope)", "Sudden collapse" },
                SuggestedQuestions: new[]
                {
                    "Did you experience palpitations, chest pain, or blurred vision immediately before fainting?",
                    "Did you hit your head or injure any limbs during the fall?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "I have recorded your fainting episode. Please remain lying down with your legs elevated if possible to assist circulation."
            );
        }

        // Scenario 7: Severe Gastrointestinal Bleeding / Vomiting Blood
        if (Matches(text, "vomiting blood", "sangre", "vomité sangre", "hematemesis", "black tarry stool", "vomitando sangre", "bleeding profusely"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Upper gastrointestinal hemorrhage (Hematemesis)", "Active bleeding" },
                SuggestedQuestions: new[]
                {
                    "Are you experiencing dizziness, cold clammy skin, or lightheadedness?",
                    "Do you have a history of gastric ulcers or take blood thinners?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Active bleeding signs require immediate clinical assessment. Please rest in a comfortable position and do not ingest foods or drinks."
            );
        }

        // Scenario 8: Severe Trauma / Heavy Bleeding / Arterial Hemorrhage
        if (Matches(text, "heavy bleeding", "deep wound", "arterial", "herida profunda", "mucha sangre", "corte profundo", "sangrado abundante"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Major acute hemorrhage", "Traumatic laceration" },
                SuggestedQuestions: new[]
                {
                    "Can you apply firm, continuous pressure directly over the wound with a clean cloth?",
                    "Does the bleeding slow down when direct pressure is applied?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Please apply firm and continuous direct pressure to the wound with clean cloth or gauze. Keep the injured area elevated if safe to do so."
            );
        }

        // Scenario 9: Sudden Loss of Vision / Eye Trauma
        if (Matches(text, "lost vision", "blind in one eye", "no puedo ver", "ceguera súbita", "pérdida de visión", "ojo no ve"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Acute visual acuity loss", "Sudden unilateral or bilateral blindness" },
                SuggestedQuestions: new[]
                {
                    "Is there pain associated with the vision loss, or is it painless?",
                    "Are you seeing flashes of light, severe halos, or dark shadows?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Sudden visual impairment is a critical condition. Please avoid rubbing your eyes and remain seated in a safe location."
            );
        }

        // Scenario 10: Diabetic Crisis / Hypoglycemia / Severe Confusion
        if (Matches(text, "sugar low", "hypoglycemia", "diabetic", "glucosa baja", "diabetes", "desorientado", "muy confundido", "shaking and sweating diabetes"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Acute glycemic imbalance / Hypoglycemia", "Neuroglycopenic confusion / Diaphoresis" },
                SuggestedQuestions: new[]
                {
                    "If you have a glucometer available, what is your current blood sugar reading?",
                    "Are you able to swallow a fast-acting carbohydrate (such as fruit juice or glucose tablets) safely?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "I have registered your diabetic symptoms. If you are conscious and able to swallow, consume 15g of fast-acting glucose and stay seated."
            );
        }

        // ---------------------------------------------------------------------
        // Group B: Urgent Scenarios (Urgency: urgent)
        // ---------------------------------------------------------------------

        // Scenario 11: High Fever with Shivering / Chills
        if (Matches(text, "high fever", "fiebre alta", "39", "40", "chills", "escalofríos", "temblores de fiebre"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "High-grade hyperthermia / Pyrexia", "Rigors / Febrile chills" },
                SuggestedQuestions: new[]
                {
                    "What is your exact temperature measured on a thermometer?",
                    "Are you experiencing a stiff neck, confusion, or a new skin rash?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have noted your high fever. Keep well-hydrated, dress in light clothing, and monitor your temperature frequently."
            );
        }

        // Scenario 12: Severe Vertigo / Acute Dizziness
        if (Matches(text, "dizzy", "dizziness", "vertigo", "spinning", "mareo", "mareado", "todo me da vueltas", "inestabilidad"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Severe vertigo / Vestibular imbalance", "Postural instability" },
                SuggestedQuestions: new[]
                {
                    "Does the room spinning sensation worsen when you turn your head or change position?",
                    "Are you having any ringing in your ears (tinnitus) or nausea?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have registered your vertigo. Please remain seated or lie down to prevent falls, and avoid rapid head movements."
            );
        }

        // Scenario 13: Palpitations / Racing Irregular Heartbeat
        if (Matches(text, "palpitations", "racing heart", "heart beating fast", "corazón acelerado", "palpitaciones", "taquicardia", "latidos irregulares"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Cardiac palpitations", "Tachyarrhythmia sensation" },
                SuggestedQuestions: new[]
                {
                    "Does your heartbeat feel unusually rapid, skipped, or fluttering?",
                    "Did this start suddenly at rest or following physical activity/caffeine?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have noted your elevated heart rate. Please sit down comfortably, breathe slowly and deeply, and avoid caffeine or stimulants."
            );
        }

        // Scenario 14: Severe Fall / Hip or Limb Injury with Inability to Walk
        if (Matches(text, "fell", "caí", "caida", "hip pain", "cannot walk", "sprain", "esguince", "no puedo pisar", "golpe fuerte cadera", "fracture"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Acute post-fall orthopedic trauma", "Inability to bear weight" },
                SuggestedQuestions: new[]
                {
                    "Are you able to move your toes and feel sensation in the injured leg?",
                    "Is there visible swelling, bruising, or deformity around the joint?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have logged your fall injury. Do not attempt to force weight on the injured limb; rest with the area supported and immobilized."
            );
        }

        // Scenario 15: Severe Burn / Scald
        if (Matches(text, "burned", "quemadura", "scald", "agua hirviendo", "quemé", "blisters", "ampollas"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Thermal burn injury", "Blistering / Integumentary damage" },
                SuggestedQuestions: new[]
                {
                    "Is the burn larger than the palm of your hand, or located on the face, hands, or joints?",
                    "Have you cooled the area with gently running cool water (not ice)?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "Please cool the burn area under gentle cool tap water for 10-20 minutes. Do not apply ice, butter, or puncture any blisters."
            );
        }

        // Scenario 16: Acute Urinary Tract / Renal Colic Pain
        if (Matches(text, "urination pain", "burning when I pee", "renal", "riñón", "dolor al orinar", "cistitis", "flank pain", "dolor lumbar fuerte"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Dysuria / Urinary tract discomfort", "Flank / Renal area discomfort" },
                SuggestedQuestions: new[]
                {
                    "Are you noticing blood in the urine or having accompanying fever/chills?",
                    "Does the pain come in sharp waves radiating toward the groin?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have registered your urinary symptoms. Drink plenty of water and arrange a prompt clinical consultation."
            );
        }

        // Scenario 17: Persistent Vomiting / Dehydration Risk
        if (Matches(text, "persistent vomiting", "vomitando todo", "no retengo líquidos", "cannot keep water down", "dehydrated", "deshidratación"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Intractable emesis", "Dehydration risk" },
                SuggestedQuestions: new[]
                {
                    "For how many hours has the vomiting been occurring without keeping fluids down?",
                    "Are you experiencing extreme thirst, dry mouth, or dark concentrated urine?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have recorded your vomiting symptoms. Take tiny, frequent sips of electrolyte solution or water every few minutes."
            );
        }

        // Scenario 18: Acute Lumbar Back Spasm / Locked Back
        if (Matches(text, "back locked", "severe back spasm", "espalda trabada", "dolor lumbar agudo", "lumbago", "ciática", "no me puedo doblar"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Acute lumbosacral spasm", "Severe functional mobility limitation" },
                SuggestedQuestions: new[]
                {
                    "Does the pain shoot down into your leg, foot, or cause numbness in the groin?",
                    "Are you having any difficulty controlling your bladder or bowel function?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "I have logged your acute back spasm. Rest on a firm surface with pillows supporting your knees, and avoid twisting."
            );
        }

        // ---------------------------------------------------------------------
        // Group C: Mild Scenarios (Urgency: mild)
        // ---------------------------------------------------------------------

        // Scenario 19: Mild Tension Headache / Eye Strain
        if (Matches(text, "mild headache", "tension headache", "dolor de cabeza leve", "cansancio visual", "headache from screen", "dolor cabeza leve"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Mild tension-type headache", "Digital eye fatigue" },
                SuggestedQuestions: new[]
                {
                    "Have you been looking at computer/phone screens for extended periods today?",
                    "Have you had enough water and rest in the last several hours?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I have noted your mild headache. Taking a short break in a quiet space and drinking a glass of water may provide relief."
            );
        }

        // Scenario 20: Mild Cold / Scratchy Sore Throat / Sneezing
        if (Matches(text, "sore throat", "garganta", "mild cold", "resfriado", "gripe leve", "estornudos", "congestión leve", "scratchy throat"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Mild upper respiratory irritation / Pharyngitis", "Mild rhinitis / Nasal congestion" },
                SuggestedQuestions: new[]
                {
                    "Do you have a mild cough, runny nose, or slight fever?",
                    "How many days have you been experiencing these cold symptoms?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I have registered your cold symptoms. Warm fluids, honey, and adequate rest are recommended while monitoring for changes."
            );
        }

        // Scenario 21: Mild Muscle Soreness / Post-Workout Fatigue
        if (Matches(text, "muscle soreness", "dolor muscular", "sore muscles", "agujetas", "cansancio por ejercicio", "dolor de piernas leve"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Delayed onset muscle soreness (DOMS)", "Physical exertion fatigue" },
                SuggestedQuestions: new[]
                {
                    "Did you engage in strenuous physical activity or unfamiliar exercises recently?",
                    "Does gentle stretching or applying a warm compress help relieve the soreness?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "Muscle soreness following exertion is common. Light stretching, hydration, and restful sleep usually help recovery."
            );
        }

        // Scenario 22: Mild Insomnia / Tiredness / Poor Sleep
        if (Matches(text, "tired", "fatigue", "insomnia", "no pude dormir", "cansado", "mal descanso", "sueño ligero"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "General fatigue", "Transient sleep disturbance" },
                SuggestedQuestions: new[]
                {
                    "How many hours of restful sleep were you able to get last night?",
                    "Are you experiencing any other physical discomfort alongside the tiredness?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I have logged your fatigue. Ensure you stay well hydrated today and consider resting in a quiet environment when possible."
            );
        }

        // Scenario 23: Mild Skin Rash / Itchiness / Insect Bite
        if (Matches(text, "itchy", "rash", "picazón", "picadura", "granito", "alergia leve en piel", "insect bite"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Localized pruritus / Mild skin erythema", "Superficial insect bite reaction" },
                SuggestedQuestions: new[]
                {
                    "Is the rash confined to a small spot, or spreading across other areas of your body?",
                    "Are you experiencing any facial swelling or difficulty breathing?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I have recorded your localized skin itch. Avoid scratching the area and apply a cool, damp compress if needed."
            );
        }

        // Scenario 24: Minor Superficial Scrape / Small Paper Cut
        if (Matches(text, "small cut", "scrape", "rasguño", "corte pequeño", "corte de papel", "raspón leve"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Minor superficial abrasion / Cut" },
                SuggestedQuestions: new[]
                {
                    "Has the small cut stopped bleeding after gentle pressure?",
                    "Have you washed the area with clean water and mild soap?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "I have noted your minor scrape. Washing the area gently with water and mild soap and applying a clean bandage is recommended."
            );
        }

        // ---------------------------------------------------------------------
        // General Fallback Scenario for unstructured input
        // ---------------------------------------------------------------------
        return new SymptomInterpretationResult(
            DetectedSymptoms: new[] { $"Reported symptom: {input.Trim()}" },
            SuggestedQuestions: new[]
            {
                "On a scale of 1 to 10, how intense would you rate your current discomfort?",
                "How long ago did these symptoms begin?"
            },
            SuggestedUrgencyLevel: "mild",
            ConversationalResponse: $"I have recorded your report: '{input.Trim()}'. Our monitoring system is observing your vitals and standing by to assist."
        );
    }

    private static bool Matches(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
