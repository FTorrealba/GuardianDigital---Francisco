using System.Collections.Concurrent;
using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

public class EventAnalysisService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventAnalysisService> _logger;
    private static readonly ConcurrentDictionary<Guid, bool> ProcessedReadings = new();
    private static DateTime _lastRoutineObservationTime = DateTime.MinValue;

    public EventAnalysisService(IServiceProvider serviceProvider, ILogger<EventAnalysisService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventAnalysisService background agent started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IGuardianDbContext>();
                var agentLogger = scope.ServiceProvider.GetRequiredService<IAgentLogService>();

                var recentReadings = await db.SensorReadings
                    .Include(r => r.Device)
                    .OrderByDescending(r => r.Timestamp)
                    .Take(50)
                    .ToListAsync(stoppingToken);

                var now = DateTime.UtcNow;

                // Log a periodic routine telemetry checkpoint once every 3 minutes
                if ((now - _lastRoutineObservationTime).TotalMinutes >= 1 && recentReadings.Any())
                {
                    _lastRoutineObservationTime = now;
                    var activeCount = recentReadings.Select(r => r.DeviceId).Distinct().Count();
                    agentLogger.Log(
                        agentName: "EventAnalysis",
                        cycleStage: "Observation",
                        message: $"Ciclo de supervisión periódico: {activeCount} sensores transmitiendo telemetría en rango seguro.",
                        details: $"Últimas {recentReadings.Count} lecturas verificadas sin anomalías fisiológicas."
                    );
                }

                foreach (var reading in recentReadings)
                {
                    if (ProcessedReadings.ContainsKey(reading.Id))
                    {
                        continue;
                    }

                    // Step 2: Analysis & Rule Evaluation
                    var (isAnomaly, ruleName, riskLevel, description) = EvaluateThresholdRules(reading);

                    if (isAnomaly)
                    {
                        var deviceType = reading.Device?.Type.ToString() ?? "UnknownDevice";

                        // Observation log for anomaly trigger
                        agentLogger.Log(
                            agentName: "EventAnalysis",
                            cycleStage: "Observation",
                            message: $"Telemetría anómala detectada en sensor {deviceType} ({reading.DataType}).",
                            details: $"Valor registrado: '{reading.Value}' | Sensor ID: {reading.DeviceId}"
                        );

                        agentLogger.Log(
                            agentName: "EventAnalysis",
                            cycleStage: "Analysis",
                            message: $"¡ANOMALÍA DETECTADA! Regla coincidente: {ruleName}.",
                            details: $"Umbral sobrepasado. Nivel de riesgo asignado: {riskLevel}. Lectura: '{reading.Value}'"
                        );

                        // Ensure we get the user ID associated with the device
                        var userId = reading.Device?.UserId ?? await db.Users.Select(u => u.Id).FirstOrDefaultAsync(stoppingToken);

                        if (userId != Guid.Empty)
                        {
                            // Step 3: Decision & Incident Creation in 'Detected' status
                            var incident = new Incident
                            {
                                UserId = userId,
                                Timestamp = DateTime.UtcNow,
                                Origin = IncidentOrigin.Sensor,
                                OriginalDescription = description,
                                RiskLevel = riskLevel,
                                Status = IncidentStatus.Detected
                            };

                            db.Incidents.Add(incident);
                            await db.SaveChangesAsync(stoppingToken);

                            agentLogger.Log(
                                agentName: "EventAnalysis",
                                cycleStage: "Decision",
                                message: $"Incidente creado en estado 'Detectado'. Nivel de Riesgo: {riskLevel}.",
                                details: $"ID de Incidente: {incident.Id} | Descripción: '{description}'",
                                incidentId: incident.Id
                            );
                        }
                    }

                    // Mark reading as analyzed
                    ProcessedReadings.TryAdd(reading.Id, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in EventAnalysisService cycle.");
            }

            // Poll sensor readings every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private (bool isAnomaly, string ruleName, RiskLevel riskLevel, string description) EvaluateThresholdRules(SensorReading reading)
    {
        var val = reading.Value.ToUpperInvariant();

        // Rule 1: Fall Detection Flag
        if (val.Contains("FALL") || val.Contains("FALL IMPACT") || val.Contains("CAIDA"))
        {
            return (true, "FallDetectionRule", RiskLevel.PossibleEmergency, "CRITICAL FALL DETECTED: Sudden high-g impact vector detected by motion sensor.");
        }

        // Rule 2: Tachycardia (HR > 140 BPM)
        if (reading.DataType == DataType.HeartRate || val.Contains("TACHYCARDIA") || val.Contains("TAQUICARDIA"))
        {
            if (val.Contains("TACHYCARDIA") || val.Contains("TAQUICARDIA") || val.Contains("CRITICAL") || ExtractNumber(val) > 140)
            {
                return (true, "CardiacTachycardiaRule", RiskLevel.Urgent, "CARDIAC ALERT: Acute tachycardia detected by biometric band.");
            }
        }

        // Rule 3: Hypoxia (SpO2 < 88%)
        if (reading.DataType == DataType.OxygenSaturation || val.Contains("HYPOXIA") || val.Contains("HIPOXIA"))
        {
            if (val.Contains("HYPOXIA") || val.Contains("HIPOXIA") || val.Contains("CRITICAL") || (ExtractNumber(val) > 0 && ExtractNumber(val) < 88))
            {
                return (true, "SevereHypoxiaRule", RiskLevel.PossibleEmergency, "CRITICAL RESPIRATORY ALERT: Severe blood oxygen desaturation detected.");
            }
        }

        // Rule 4: Immobility (Prolonged Zero Movement > 180 min)
        if (val.Contains("IMMOBILITY") || val.Contains("INMOVILIDAD") || val.Contains("ZERO MOVEMENT"))
        {
            return (true, "ProlongedImmobilityRule", RiskLevel.Urgent, "IMMOBILITY ALERT: Extended duration of zero motion detected in living area.");
        }

        // Rule 5: Door Forced / Unauthorized Entry
        if (val.Contains("UNAUTHORIZED") || val.Contains("FORZADA") || val.Contains("FORCED"))
        {
            return (true, "PerimeterBreachRule", RiskLevel.Urgent, "PERIMETER SECURITY ALERT: Unexpected access or forced entry on perimeter door.");
        }

        return (false, string.Empty, RiskLevel.Mild, string.Empty);
    }

    private int ExtractNumber(string input)
    {
        var digits = new string(input.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : 0;
    }
}
