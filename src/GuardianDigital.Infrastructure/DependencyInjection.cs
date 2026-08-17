using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Infrastructure.Persistence;
using GuardianDigital.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GuardianDigital.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=guardian.db";

        services.AddDbContext<GuardianDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IGuardianDbContext>(provider => provider.GetRequiredService<GuardianDbContext>());

        // Register Agent Audit Log Service
        services.AddSingleton<IAgentLogService, AgentLogService>();

        // Register LLM Symptom Interpretation Service
        services.AddSingleton<ILanguageModelService, LanguageModelService>();

        // Register Preliminary Medical Evaluation Agent Service
        services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();

        // Register Emergency Management Agent Service
        services.AddScoped<IEmergencyManagementService, EmergencyManagementService>();

        // Register Persistent Memory & Learning Agent Service
        services.AddScoped<ILearningStatsService, LearningStatsService>();

        // Register Background Services
        services.AddHostedService<SensorSimulatorService>();
        services.AddHostedService<EventAnalysisService>();

        return services;
    }
}
