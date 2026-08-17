using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GuardianDigital.Application.Features.Assistant;

public static class AssistantFeature
{
    public static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/assistant/status", () => Results.Ok(new { AiEngine = "Guardián Digital LLM Core", Mode = "Active" }))
           .WithTags("Assistant");
    }
}
