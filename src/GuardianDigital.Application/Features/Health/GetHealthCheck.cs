using GuardianDigital.Application.Common.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GuardianDigital.Application.Features.Health;

public static class GetHealthCheck
{
    public record Response(string Status, string Message, DateTime Timestamp, int DatabaseRecordCount);

    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (IGuardianDbContext db) =>
        {
            int count = 0;
            try
            {
                count = await db.SystemHealthChecks.CountAsync();
            }
            catch
            {
                // Graceful fallback during cold start
            }

            var response = new Response(
                Status: "Healthy",
                Message: "Guardián Digital API is operational",
                Timestamp: DateTime.UtcNow,
                DatabaseRecordCount: count
            );

            return Results.Ok(response);
        })
        .WithName("GetHealthCheck")
        .WithTags("Health");
    }
}
