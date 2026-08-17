using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Dashboard;

public static class InjectAnomaly
{
    public record InjectAnomalyRequest(
        Guid? DeviceId,
        string AnomalyType
    );

    public static async Task<IResult> HandleAsync(InjectAnomalyRequest request, IGuardianDbContext db)
    {
        LinkedDevice? device = null;

        if (request.DeviceId.HasValue)
        {
            device = await db.LinkedDevices.FirstOrDefaultAsync(d => d.Id == request.DeviceId.Value);
        }

        var anomaly = request.AnomalyType?.Trim() ?? "Fall";

        if (device == null)
        {
            var targetType = anomaly.ToLower() switch
            {
                "tachycardia" => DeviceType.BiometricBand,
                "hypoxia" => DeviceType.PulseOximeter,
                "immobility" or "fall" => DeviceType.MotionSensor,
                "doorforced" => DeviceType.DoorSensor,
                _ => DeviceType.MotionSensor
            };

            device = await db.LinkedDevices.FirstOrDefaultAsync(d => d.Type == targetType)
                     ?? await db.LinkedDevices.FirstOrDefaultAsync();
        }

        if (device == null)
        {
            return Results.BadRequest(new { error = "No active devices available to inject anomaly into. Please seed devices first." });
        }

        DataType dataType;
        string value;

        switch (anomaly.ToLower())
        {
            case "tachycardia":
                dataType = DataType.HeartRate;
                value = "172 BPM [CRITICAL TACHYCARDIA]";
                break;
            case "hypoxia":
                dataType = DataType.OxygenSaturation;
                value = "81% SpO2 [SEVERE HYPOXIA]";
                break;
            case "immobility":
                dataType = DataType.Motion;
                value = "Zero Movement Detected for 240 Minutes [PROLONGED IMMOBILITY]";
                break;
            case "doorforced":
                dataType = DataType.DoorOpening;
                value = "UNAUTHORIZED FORCED ENTRY DETECTED AT FRONT DOOR";
                break;
            case "fall":
            default:
                dataType = DataType.Motion;
                value = "CRITICAL FALL IMPACT DETECTED (5.2G Acceleration Vector)";
                break;
        }

        var reading = new SensorReading
        {
            DeviceId = device.Id,
            Timestamp = DateTime.UtcNow,
            DataType = dataType,
            Value = value
        };

        device.LastReading = reading.Timestamp;
        db.SensorReadings.Add(reading);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            message = $"Anomalous event '{anomaly}' successfully injected into device {device.Type}.",
            readingId = reading.Id,
            deviceId = device.Id,
            deviceType = device.Type.ToString(),
            dataType = dataType.ToString(),
            value = value,
            timestamp = reading.Timestamp
        });
    }
}
