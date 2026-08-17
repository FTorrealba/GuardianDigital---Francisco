using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GuardianDigital.Application.Features.Dashboard;

public static class DashboardFeature
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/devices", (IGuardianDbContext db) =>
            GetDevices.HandleAsync(db))
            .WithName("GetDevices")
            .WithTags("Devices");

        app.MapPost("/api/devices/seed", (Guid? userId, IGuardianDbContext db) =>
            SeedDevices.HandleAsync(userId, db))
            .WithName("SeedDevices")
            .WithTags("Devices");

        app.MapPost("/api/devices/inject-anomaly", (InjectAnomaly.InjectAnomalyRequest request, IGuardianDbContext db) =>
            InjectAnomaly.HandleAsync(request, db))
            .WithName("InjectAnomaly")
            .WithTags("Devices");

        app.MapGet("/api/learning/stats", (Guid? userId, ILearningStatsService learningStatsService, CancellationToken ct) =>
            GetLearningStats.HandleAsync(userId, learningStatsService, ct))
            .WithName("GetLearningStats")
            .WithTags("Dashboard");
    }
}
