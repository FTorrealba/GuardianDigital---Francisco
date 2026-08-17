namespace GuardianDigital.Application.Common.Interfaces;

public record HourlyActivityDto(
    int Hour, // 0..23
    int ReadingCount,
    string ActivityCategory // "Low" | "Moderate" | "Peak"
);

public record LearningStatsDto(
    int TotalIncidentsLast30Days,
    int FalseAlarmsLast30Days,
    double FalseAlarmPercentage,
    double PrecisionScore,
    Dictionary<string, int> IncidentsByOrigin,
    Dictionary<string, int> IncidentsByRiskLevel,
    Dictionary<string, int> IncidentsByStatus,
    IReadOnlyList<HourlyActivityDto> HourlyActivityDistribution,
    string PeakActivityWindow,
    string RestActivityWindow,
    int TotalTelemetryReadingsAnalyzed,
    DateTime ComputedAt
);

public interface ILearningStatsService
{
    /// <summary>
    /// Computes behavioral learning metrics from database history:
    /// - False alarm percentage over the last 30 days
    /// - Incidents distribution by origin, risk level, and status
    /// - Typical daily activity hours distribution and peak/rest windows from telemetry readings
    /// </summary>
    Task<LearningStatsDto> GetLearningStatsAsync(
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
