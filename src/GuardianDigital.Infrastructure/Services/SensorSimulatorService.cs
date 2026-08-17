using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GuardianDigital.Infrastructure.Services;

public class SensorSimulatorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SensorSimulatorService> _logger;
    private readonly Random _random = new();

    public SensorSimulatorService(IServiceProvider serviceProvider, ILogger<SensorSimulatorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SensorSimulatorService background telemetry generator started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IGuardianDbContext>();

                var activeDevices = await db.LinkedDevices
                    .Where(d => d.Status == DeviceStatus.Active)
                    .ToListAsync(stoppingToken);

                if (activeDevices.Any())
                {
                    var now = DateTime.UtcNow;

                    foreach (var device in activeDevices)
                    {
                        var (dataType, value) = GenerateNormalReading(device.Type);

                        var reading = new SensorReading
                        {
                            DeviceId = device.Id,
                            Timestamp = now,
                            DataType = dataType,
                            Value = value
                        };

                        device.LastReading = now;
                        db.SensorReadings.Add(reading);
                    }

                    // Retention cleanup: purge readings older than 24 hours
                    var cutoff = now.AddHours(-24);
                    var oldReadings = await db.SensorReadings
                        .Where(r => r.Timestamp < cutoff)
                        .ToListAsync(stoppingToken);

                    if (oldReadings.Any())
                    {
                        db.SensorReadings.RemoveRange(oldReadings);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during sensor simulation cycle.");
            }

            // Transmit synthetic telemetry every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private (DataType dataType, string value) GenerateNormalReading(DeviceType deviceType)
    {
        return deviceType switch
        {
            DeviceType.BiometricBand or DeviceType.Smartwatch =>
                (DataType.HeartRate, $"{_random.Next(68, 82)} BPM [Normal Resting]"),

            DeviceType.PulseOximeter =>
                (DataType.OxygenSaturation, $"{_random.Next(96, 99)}% SpO2 [Optimal]"),

            DeviceType.MotionSensor =>
                (DataType.Motion, $"Normal Micro-Movement ({(_random.NextDouble() * 0.15 + 0.05):F2}G)"),

            DeviceType.DoorSensor =>
                (DataType.DoorOpening, "Closed"),

            DeviceType.Microphone =>
                (DataType.Audio, $"Normal Ambient ({_random.Next(32, 42)} dB)"),

            DeviceType.Camera or _ =>
                (DataType.Video, "Clear Feed - Normal Activity")
        };
    }
}
