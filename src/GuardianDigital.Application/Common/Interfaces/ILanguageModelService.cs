namespace GuardianDigital.Application.Common.Interfaces;

public record SymptomInterpretationResult(
    IReadOnlyList<string> DetectedSymptoms,
    IReadOnlyList<string> SuggestedQuestions,
    string SuggestedUrgencyLevel, // "mild" | "urgent" | "possible_emergency"
    string ConversationalResponse
);

public interface ILanguageModelService
{
    /// <summary>
    /// Interprets free text user-reported symptoms using natural language clinical triage models or scenarios.
    /// </summary>
    Task<SymptomInterpretationResult> InterpretSymptomAsync(string userMessage, CancellationToken cancellationToken = default);
}
