using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Dashboard;

public static class GetDevices
{
    public record DeviceDto(
        Guid Id,
        Guid UserId,
        string Type,
        string Status,
        DateTime? LastReading,
        bool IsTransmitting,
        List<ReadingDto> RecentReadings
    );

    public record ReadingDto(
        Guid Id,
        DateTime Timestamp,
        string DataType,
        string Value
    );

    public static async Task<IResult> HandleAsync(IGuardianDbContext db)
    {
        var devices = await db.LinkedDevices
            .Include(d => d.Readings.OrderByDescending(r => r.Timestamp).Take(10))
            .ToListAsync();

        var dtos = devices.Select(d => new DeviceDto(
            d.Id,
            d.UserId,
            d.Type.ToString(),
            d.Status.ToString(),
            d.LastReading ?? d.Readings.FirstOrDefault()?.Timestamp,
            d.Status == Domain.Enums.DeviceStatus.Active,
            d.Readings.Select(r => new ReadingDto(
                r.Id,
                r.Timestamp,
                r.DataType.ToString(),
                r.Value
            )).ToList()
        )).ToList();

        return Results.Ok(dtos);
    }
}
