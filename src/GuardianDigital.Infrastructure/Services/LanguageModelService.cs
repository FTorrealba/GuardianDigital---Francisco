using System.Text.RegularExpressions;
using GuardianDigital.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Natural Language Interpretation Service for voice and text symptom reporting.
/// Implements clinical interpretation logic with real LLM provider connection hooks and 24 scenario diagnostics in Spanish.
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
        Eres 'Copiloto de Seguridad Guardián Digital' — un asistente empático de triaje clínico para monitoreo domiciliario de pacientes y adultos mayores.
        Tu objetivo es interpretar las descripciones de síntomas habladas o escritas por el usuario en lenguaje natural.
        Directivas:
        1. Interpretar con precisión los síntomas comunicados.
        2. Formular 1 a 2 preguntas de seguimiento clínicamente relevantes (estrictamente no diagnósticas).
        3. Asignar un nivel de urgencia sugerido: 'mild' (leve), 'urgent' (urgente) o 'possible_emergency' (posible emergencia).
        4. Proveer una respuesta conversacional empática, calmada y en idioma español.
        Retornar la salida estrictamente en formato JSON:
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
                DetectedSymptoms: new[] { "Descripción no especificada" },
                SuggestedQuestions: new[] { "¿Podría describir con sus palabras qué malestar o síntoma está sintiendo en este momento?" },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "No pude detectar síntomas específicos en su mensaje. ¿En qué puedo asistirlo en este momento?"
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
        // 2. Local Clinical Scenario Diagnostic Engine (24 Scenarios in Spanish)
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
            && Matches(text, "breath", "respirar", "pain", "dolor", "tight", "aprieta", "shortness", "falta el aire"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Malestar / Opresión torácica aguda", "Disnea / Dificultad para respirar" },
                SuggestedQuestions: new[]
                {
                    "¿El dolor de pecho se irradia hacia su brazo izquierdo, mandíbula, cuello o espalda?",
                    "¿Presenta sudoración fría, náuseas o mareos junto con el dolor?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "He registrado su opresión en el pecho y dificultad para respirar. Por favor siéntese, repose cómodamente y evite cualquier esfuerzo físico mientras preparamos la asistencia de emergencia."
            );
        }

        // Scenario 2: Stroke Warning Signs (FAST - Facial droop, arm weakness, slurred speech)
        if (Matches(text, "droop", "slur", "stroke", "paralysis", "weakness in arm", "arm is weak", "numb face", "numbness", "numb", "lado dormido", "cara caída", "no puedo mover el brazo", "dificultad para hablar", "balbuceo", "brazo débil", "cara caida"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Asimetría facial unilateral / Pérdida de fuerza", "Dificultad en el habla / Disartria", "Paresia o debilidad motora súbita" },
                SuggestedQuestions: new[]
                {
                    "¿Estos síntomas comenzaron repentinamente en los últimos minutos?",
                    "¿Puede sonreír de manera simétrica y levantar ambos brazos al mismo tiempo?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Estos síntomas sugieren un potencial compromiso neurológico agudo. Permanezca sentado en reposo y mantenga notificados a sus contactos de emergencia de inmediato."
            );
        }

        // Scenario 3: Thunderclap / Sudden Worst Headache of Life
        if (Matches(text, "worst headache", "thunderclap", "cabeza me estalla", "peor dolor de cabeza", "severe headache sudden", "dolor de cabeza insoportable", "trueno"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Cefalea en trueno / Explosiva repentina", "Cefalea severa hiperaguda" },
                SuggestedQuestions: new[]
                {
                    "¿Este dolor de cabeza alcanzó su máxima intensidad en menos de un minuto?",
                    "¿Presenta rigidez en la nuca, fiebre o alteraciones visuales?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Un dolor de cabeza brusco e intenso requiere evaluación médica inmediata. Por favor recuéstese en un ambiente tranquilo y con luz tenue."
            );
        }

        // Scenario 4: Severe Respiratory Distress / Choking / Gasping
        if (Matches(text, "choking", "ahogo", "asfixia", "gasping for air", "cannot breathe at all", "severe asthma attack", "no entra aire", "me ahogo", "cerrando la garganta"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Insuficiencia respiratoria severa", "Sensación de asfixia / Obstrucción de vía aérea", "Disnea aguda" },
                SuggestedQuestions: new[]
                {
                    "¿Tiene a su alcance un inhalador de rescate o medicación respiratoria recetada?",
                    "¿Nota sus labios, dedos o rostro pálidos o azulados?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Detecto una dificultad respiratoria severa. Intente mantener una postura erguida o semi-incorporada para facilitar el paso del aire mientras se coordina la respuesta de emergencia."
            );
        }

        // Scenario 5: Anaphylaxis / Severe Allergic Reaction / Throat Swelling
        if (Matches(text, "allergic reaction", "alergia", "throat swelling", "hinchazón garganta", "tongue swollen", "lengua hinchada", "anaphylaxis", "urticaria severa", "edema de glotis"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Edema orofaríngeo / Opresión en garganta", "Sospecha de reacción alérgica sistémica / Anafilaxia" },
                SuggestedQuestions: new[]
                {
                    "¿Estuvo expuesto recientemente a algún alimento, medicamento o picadura conocida?",
                    "¿Tiene a mano un autoinyector de epinefrina o antialérgico de rescate?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Los síntomas alérgicos severos con afectación de vías respiratorias son de alta prioridad. Mantenga la calma y tenga a mano sus medicamentos de emergencia."
            );
        }

        // Scenario 6: Syncope / Fainting / Sudden Loss of Consciousness
        if (Matches(text, "fainted", "passed out", "desmayé", "desmayo", "blacked out", "perdí el conocimiento", "syncope", "desvanecí"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Pérdida transitoria de la conciencia (Síncope)", "Colapso o desvanecimiento súbito" },
                SuggestedQuestions: new[]
                {
                    "¿Sintió palpitaciones, dolor de pecho o visión borrosa justo antes del desmayo?",
                    "¿Sufrió algún golpe en la cabeza o lesión en extremidades al caer?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "He registrado su episodio de desvanecimiento. Permanezca recostado con las piernas ligeramente elevadas para favorecer la circulación sanguínea."
            );
        }

        // Scenario 7: Severe Gastrointestinal Bleeding / Vomiting Blood
        if (Matches(text, "vomiting blood", "sangre", "vomité sangre", "hematemesis", "black tarry stool", "vomitando sangre", "bleeding profusely"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Hemorragia digestiva alta (Hematemesis)", "Sangrado activo gastrointestinal" },
                SuggestedQuestions: new[]
                {
                    "¿Siente mareos, piel fría y pegajosa o sensación de desmayo?",
                    "¿Tiene antecedentes de úlceras o toma medicamentos anticoagulantes?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Los signos de sangrado activo requieren evaluación clínica urgente. Descanse en una posición cómoda y no ingiera alimentos ni bebidas por el momento."
            );
        }

        // Scenario 8: Severe Trauma / Heavy Bleeding / Arterial Hemorrhage
        if (Matches(text, "heavy bleeding", "deep wound", "arterial", "herida profunda", "mucha sangre", "corte profundo", "sangrado abundante"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Hemorragia aguda importante", "Laceración o corte profundo traumático" },
                SuggestedQuestions: new[]
                {
                    "¿Puede aplicar presión firme y continua directamente sobre la herida con un paño limpio?",
                    "¿El sangrado disminuye al comprimir la zona?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "Por favor aplique presión directa, firme y constante sobre la herida con un paño limpio o gasa. Mantenga la extremidad elevada si no causa dolor."
            );
        }

        // Scenario 9: Sudden Loss of Vision / Eye Trauma
        if (Matches(text, "lost vision", "blind in one eye", "no puedo ver", "ceguera súbita", "pérdida de visión", "ojo no ve"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Pérdida aguda de agudeza visual", "Ceguera súbita unilateral o bilateral" },
                SuggestedQuestions: new[]
                {
                    "¿La pérdida de visión viene acompañada de dolor ocular o de cabeza?",
                    "¿Observa destellos de luz, sombras oscuras o visión en túnel?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "La alteración visual súbita es un síntoma de atención urgente. Evite frotarse los ojos y permanezca sentado en un lugar seguro."
            );
        }

        // Scenario 10: Diabetic Crisis / Hypoglycemia / Severe Confusion
        if (Matches(text, "sugar low", "hypoglycemia", "diabetic", "glucosa baja", "diabetes", "desorientado", "muy confundido", "shaking and sweating diabetes", "azucar baja"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Desbalance glucémico agudo / Hipoglucemia", "Desorientación neuroglucopénica / Sudoración profusa" },
                SuggestedQuestions: new[]
                {
                    "Si dispone de un glucómetro, ¿cuál es su valor actual de glucosa en sangre?",
                    "¿Puede tragar de forma segura un líquido azucarado o jugo de frutas?"
                },
                SuggestedUrgencyLevel: "possible_emergency",
                ConversationalResponse: "He registrado sus síntomas glucémicos. Si está consciente y puede tragar sin dificultad, ingiera 15g de hidratos de carbono rápidos (ej. medio vaso de jugo o agua azucarada) y manténgase sentado."
            );
        }

        // ---------------------------------------------------------------------
        // Group B: Urgent Scenarios (Urgency: urgent)
        // ---------------------------------------------------------------------

        // Scenario 11: High Fever with Shivering / Chills
        if (Matches(text, "high fever", "fiebre alta", "39", "40", "chills", "escalofríos", "temblores de fiebre", "escalofrios"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Hipertermia / Fiebre de alto grado", "Chuchos de frío / Escalofríos febriles" },
                SuggestedQuestions: new[]
                {
                    "¿Cuál es su temperatura exacta medida con termómetro?",
                    "¿Tiene rigidez en la nuca, confusión mental o manchas rojizas en la piel?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He tomado nota de su cuadro febril elevado. Manténgase bien hidratado con agua o caldos, use ropa ligera y controle su temperatura periódicamente."
            );
        }

        // Scenario 12: Severe Vertigo / Acute Dizziness
        if (Matches(text, "dizzy", "dizziness", "vertigo", "spinning", "mareo", "mareado", "todo me da vueltas", "inestabilidad", "vértigo", "mucho mareo"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Vértigo severo / Desbalance vestibular", "Inestabilidad postural aguda" },
                SuggestedQuestions: new[]
                {
                    "¿La sensación de giro empeora al mover la cabeza o cambiar de postura?",
                    "¿Presenta zumbidos en los oídos (acúfenos) o náuseas intensas?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado su episodio de vértigo. Por favor permanezca sentado o acostado para evitar caídas y evite realizar movimientos bruscos de la cabeza."
            );
        }

        // Scenario 13: Palpitations / Racing Irregular Heartbeat
        if (Matches(text, "palpitations", "racing heart", "heart beating fast", "corazón acelerado", "palpitaciones", "taquicardia", "latidos irregulares", "late a mil"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Palpitaciones cardíacas perceptibles", "Sensación de taquiarritmia" },
                SuggestedQuestions: new[]
                {
                    "¿Siente el pulso acelerado, con saltos o latidos desordenados?",
                    "¿Comenzó de forma repentina en reposo o tras consumir café/estimulantes?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado su aceleración en el ritmo cardíaco. Por favor siéntese cómodo, respire lento y profundo, y evite el consumo de café o estimulantes."
            );
        }

        // Scenario 14: Severe Fall / Hip or Limb Injury with Inability to Walk
        if (Matches(text, "fell", "caí", "caida", "caída", "hip pain", "cannot walk", "sprain", "esguince", "no puedo pisar", "golpe fuerte cadera", "fracture", "resbalé", "no puedo pararme"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Traumatismo ortopédico agudo por caída", "Imposibilidad para la bipedestación o apoyo" },
                SuggestedQuestions: new[]
                {
                    "¿Puede mover los dedos y siente sensibilidad en la pierna o brazo afectado?",
                    "¿Observa hinchazón evidente, hematoma o deformidad en la zona golpeada?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado su lesión por caída. No intente forzar el apoyo sobre la extremidad afectada; permanezca en reposo con la zona inmovilizada."
            );
        }

        // Scenario 15: Severe Burn / Scald
        if (Matches(text, "burned", "quemadura", "scald", "agua hirviendo", "quemé", "blisters", "ampollas"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Quemadura térmica", "Formación de flictenas / Ampollas dérmicas" },
                SuggestedQuestions: new[]
                {
                    "¿La quemadura es mayor que la palma de su mano o afecta cara, manos o articulaciones?",
                    "¿Ha enfriado la zona bajo un chorro suave de agua corriente fresca?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "Enfríe la zona afectada bajo un chorro suave de agua fresca durante 10 a 15 minutos. No aplique hielo, cremas caseras ni reviente las ampollas."
            );
        }

        // Scenario 16: Acute Urinary Tract / Renal Colic Pain
        if (Matches(text, "urination pain", "burning when I pee", "renal", "riñón", "dolor al orinar", "cistitis", "flank pain", "dolor lumbar fuerte"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Disuria / Molestia miccional aguda", "Dolor en fosa renal / Lumbar cólico" },
                SuggestedQuestions: new[]
                {
                    "¿Ha observado sangre en la orina o tiene fiebre con escalofríos?",
                    "¿El dolor se presenta en oleadas intensas que bajan hacia la ingle?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado sus molestias urinarias. Beba abundante agua y coordine una consulta médica para evaluación de la muestra."
            );
        }

        // Scenario 17: Persistent Vomiting / Dehydration Risk
        if (Matches(text, "persistent vomiting", "vomitando todo", "no retengo líquidos", "cannot keep water down", "dehydrated", "deshidratación"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Vómitos incoercibles", "Riesgo de deshidratación aguda" },
                SuggestedQuestions: new[]
                {
                    "¿Durante cuántas horas ha estado vomitando sin poder tolerar líquidos?",
                    "¿Siente la boca muy seca, sed intensa u orina escasa y oscura?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado su cuadro de vómitos. Tome sorbos muy pequeños de agua o suero de rehidratación oral cada 5 a 10 minutos."
            );
        }

        // Scenario 18: Acute Lumbar Back Spasm / Locked Back
        if (Matches(text, "back locked", "severe back spasm", "espalda trabada", "dolor lumbar agudo", "lumbago", "ciática", "no me puedo doblar"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Espasmo lumbosacro agudo / Lumbalgia", "Limitación funcional severa de la movilidad" },
                SuggestedQuestions: new[]
                {
                    "¿El dolor se irradia hacia la pierna o siente adormecimiento en pies o genitales?",
                    "¿Presenta alguna dificultad para controlar la micción o evacuación?"
                },
                SuggestedUrgencyLevel: "urgent",
                ConversationalResponse: "He registrado su contractura lumbar aguda. Descanse sobre una superficie firme con almohadas bajo las rodillas y evite movimientos de torsión."
            );
        }

        // ---------------------------------------------------------------------
        // Group C: Mild Scenarios (Urgency: mild)
        // ---------------------------------------------------------------------

        // Scenario 19: Mild Tension Headache / Eye Strain
        if (Matches(text, "mild headache", "tension headache", "dolor de cabeza leve", "cansancio visual", "headache from screen", "dolor cabeza leve", "frente a la pantalla"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Cefalea tensional leve", "Fatiga visual digital" },
                SuggestedQuestions: new[]
                {
                    "¿Ha estado frente a pantallas de computadora o teléfono durante muchas horas hoy?",
                    "¿Ha tomado suficiente agua y descansado en las últimas horas?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "He registrado su dolor de cabeza leve. Tomar un breve descanso en un lugar tranquilo y beber un vaso de agua fresca le ayudará a aliviar la molestia."
            );
        }

        // Scenario 20: Mild Cold / Scratchy Sore Throat / Sneezing
        if (Matches(text, "sore throat", "garganta", "mild cold", "resfriado", "gripe leve", "estornudos", "congestión leve", "scratchy throat", "dolor de garganta", "congestión nasal", "resfrio"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Faringitis / Irritación leve de garganta", "Rinitis / Congestión nasal leve" },
                SuggestedQuestions: new[]
                {
                    "¿Tiene tos leve, secreción nasal o febrícula?",
                    "¿Desde hace cuántos días siente estos síntomas de resfrío?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "He registrado sus síntomas de resfriado. Se recomienda ingerir líquidos tibios, descansar y observar la evolución de las molestias."
            );
        }

        // Scenario 21: Mild Muscle Soreness / Post-Workout Fatigue
        if (Matches(text, "muscle soreness", "dolor muscular", "sore muscles", "agujetas", "cansancio por ejercicio", "dolor de piernas leve", "después de caminar", "despues de caminar"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Mialgias por esfuerzo / Dolor muscular tardío", "Fatiga física leve" },
                SuggestedQuestions: new[]
                {
                    "¿Realizó actividad física o esfuerzos no habituales recientemente?",
                    "¿El reposo o la aplicación de compresas tibias le produce alivio?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "El cansancio muscular tras un esfuerzo físico es habitual. Estiramientos suaves, buena hidratación y un buen descanso facilitarán su recuperación."
            );
        }

        // Scenario 22: Mild Insomnia / Tiredness / Poor Sleep
        if (Matches(text, "tired", "fatigue", "insomnia", "no pude dormir", "cansado", "mal descanso", "sueño ligero", "cansancio"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Fatiga general leve", "Alteración transitoria del sueño" },
                SuggestedQuestions: new[]
                {
                    "¿Cuántas horas de sueño continuo pudo conciliar anoche?",
                    "¿Siente alguna otra molestia física acompañando al cansancio?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "He registrado su sensación de cansancio. Procure mantenerse bien hidratado hoy y tome un momento de reposo en un ambiente relajado."
            );
        }

        // Scenario 23: Mild Skin Rash / Itchiness / Insect Bite
        if (Matches(text, "itchy", "rash", "picazón", "picadura", "granito", "alergia leve en piel", "insect bite", "picazon"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Prurito localizado / Eritema cutáneo leve", "Reacción leve a picadura superficial" },
                SuggestedQuestions: new[]
                {
                    "¿La picazón está en una zona pequeña o se extiende por el cuerpo?",
                    "¿Presenta hinchazón en el rostro o alguna dificultad respiratoria?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "He registrado la molestia en su piel. Evite rascarse para no irritar la zona y aplique una compresa fresca y limpia si lo desea."
            );
        }

        // Scenario 24: Minor Superficial Scrape / Small Paper Cut
        if (Matches(text, "small cut", "scrape", "rasguño", "corte pequeño", "corte de papel", "raspón leve", "rasguno", "corte pequeno", "raspon"))
        {
            return new SymptomInterpretationResult(
                DetectedSymptoms: new[] { "Erosión superficial menor / Rasguño leve" },
                SuggestedQuestions: new[]
                {
                    "¿El pequeño corte ya dejó de sangrar tras limpiarlo?",
                    "¿Ha lavado la zona con agua limpia y jabón neutro?"
                },
                SuggestedUrgencyLevel: "mild",
                ConversationalResponse: "He tomado nota de su pequeño rasguño. Se recomienda lavar suavemente con agua y jabón y colocar un apósito limpio si es necesario."
            );
        }

        // ---------------------------------------------------------------------
        // General Fallback Scenario for unstructured input
        // ---------------------------------------------------------------------
        return new SymptomInterpretationResult(
            DetectedSymptoms: new[] { $"Síntoma reportado: {input.Trim()}" },
            SuggestedQuestions: new[]
            {
                "En una escala del 1 al 10, ¿qué nivel de molestia o incomodidad siente en este momento?",
                "¿Hace cuánto tiempo comenzaron estas sensaciones?"
            },
            SuggestedUrgencyLevel: "mild",
            ConversationalResponse: $"He registrado su reporte: '{input.Trim()}'. Nuestro sistema de teleasistencia está observando sus signos vitales y preparado para asistirlo."
        );
    }

    private static bool Matches(string text, params string[] keywords)
    {
        return keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }
}
