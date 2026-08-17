using GuardianDigital.Application.Features.Alerts;
using GuardianDigital.Application.Features.Assistant;
using GuardianDigital.Application.Features.Dashboard;
using GuardianDigital.Application.Features.Health;
using GuardianDigital.Application.Features.Onboarding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GuardianDigital.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapFeatureEndpoints(this IEndpointRouteBuilder app)
    {
        GetHealthCheck.MapEndpoint(app);
        OnboardingFeature.MapEndpoints(app);
        DashboardFeature.MapEndpoints(app);
        AlertsFeature.MapEndpoints(app);
        AssistantFeature.MapEndpoints(app);

        return app;
    }
}
