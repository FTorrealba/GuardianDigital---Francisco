using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

/// <summary>
/// Persistent Memory and Learning Agent Service.
/// Aggregates historical incident and telemetry data to compute detection precision,
/// false alarm rates, incident categorization, and typical daily activity windows.
/// </summary>
public class LearningStatsService : ILearningStatsService
{
    private readonly IGuardianDbContext _db;
    private readonly IAgentLogService _agentLogger;
    private readonly ILogger<LearningStatsService> _logger;

    public LearningStatsService(
        IGuardianDbContext db,
        IAgentLogService agentLogger,
        ILogger<LearningStatsService> logger)
    {
        _db = db;
        _agentLogger = agentLogger;
        _logger = logger;
    }

    public async Task<LearningStatsDto> GetLearningStatsAsync(Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        // 1. Incidents Query in Last 30 Days
        var query = _db.Incidents.AsQueryable();
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(i => i.UserId == userId.Value);
        }

        var incidentsLast30Days = await query
            .Where(i => i.Timestamp >= cutoff)
            .ToListAsync(cancellationToken);

        int totalIncidents30 = incidentsLast30Days.Count;
        int falseAlarms30 = incidentsLast30Days.Count(i => i.Status == IncidentStatus.FalseAlarm);

        double falseAlarmPercentage = totalIncidents30 > 0
            ? Math.Round((double)falseAlarms30 / totalIncidents30 * 100.0, 2)
            : 0.0;

        double precisionScore = Math.Round(100.0 - falseAlarmPercentage, 2);

        // 2. Incident Breakdown Dictionaries
        var allIncidents = await query.ToListAsync(cancellationToken);

        var byOrigin = allIncidents
            .GroupBy(i => i.Origin.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byRiskLevel = allIncidents
            .GroupBy(i => i.RiskLevel.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byStatus = allIncidents
            .GroupBy(i => i.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure default keys exist for UI stability
        foreach (var origin in Enum.GetNames<IncidentOrigin>())
        {
            if (!byOrigin.ContainsKey(origin)) byOrigin[origin] = 0;
        }

        foreach (var risk in Enum.GetNames<RiskLevel>())
        {
            if (!byRiskLevel.ContainsKey(risk)) byRiskLevel[risk] = 0;
        }

        foreach (var status in Enum.GetNames<IncidentStatus>())
        {
            if (!byStatus.ContainsKey(status)) byStatus[status] = 0;
        }

        // 3. Typical Activity Hours (from SensorReadings history)
        var sensorReadings = await _db.SensorReadings
            .Where(r => r.Timestamp >= cutoff)
            .ToListAsync(cancellationToken);

        int totalTelemetryCount = sensorReadings.Count;

        // Group into 24 hours (0..23)
        var hourlyCounts = new int[24];
        foreach (var r in sensorReadings)
        {
            int hour = r.Timestamp.Hour;
            if (hour >= 0 && hour < 24)
            {
                hourlyCounts[hour]++;
            }
        }

        int maxHourlyReading = hourlyCounts.Length > 0 ? hourlyCounts.Max() : 0;
        var hourlyActivityList = new List<HourlyActivityDto>();

        for (int h = 0; h < 24; h++)
        {
            int count = hourlyCounts[h];
            string category = "Low";
            if (maxHourlyReading > 0)
            {
                double ratio = (double)count / maxHourlyReading;
                if (ratio >= 0.65) category = "Peak";
                else if (ratio >= 0.25) category = "Moderate";
            }

            hourlyActivityList.Add(new HourlyActivityDto(h, count, category));
        }

        // Determine Peak and Rest Windows
        var peakHour = hourlyActivityList.OrderByDescending(h => h.ReadingCount).FirstOrDefault()?.Hour ?? 10;
        var peakWindow = $"{peakHour:D2}:00 - {((peakHour + 4) % 24):D2}:00";
        var restWindow = "23:00 - 06:00";

        // Step 4: Agent Learning Log
        _agentLogger.Log(
            agentName: "LearningAgent",
            cycleStage: "Analysis",
            message: $"Computed 30-day learning metrics. False Alarm Rate: {falseAlarmPercentage}% (Precision: {precisionScore}%).",
            details: $"Total Incidents: {totalIncidents30} (False Alarms: {falseAlarms30}) | Telemetry Analyzed: {totalTelemetryCount} records"
        );

        return new LearningStatsDto(
            TotalIncidentsLast30Days: totalIncidents30,
            FalseAlarmsLast30Days: falseAlarms30,
            FalseAlarmPercentage: falseAlarmPercentage,
            PrecisionScore: precisionScore,
            IncidentsByOrigin: byOrigin,
            IncidentsByRiskLevel: byRiskLevel,
            IncidentsByStatus: byStatus,
            HourlyActivityDistribution: hourlyActivityList,
            PeakActivityWindow: peakWindow,
            RestActivityWindow: restWindow,
            TotalTelemetryReadingsAnalyzed: totalTelemetryCount,
            ComputedAt: DateTime.UtcNow
        );
    }
}
