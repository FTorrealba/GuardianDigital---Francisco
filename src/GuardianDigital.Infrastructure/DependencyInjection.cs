using System.Text.RegularExpressions;
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
        // 1. Connection String & Database Provider Resolution
        var rawConn = configuration["DATABASE_URL"]
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=guardian.db";

        var formattedConn = FormatConnectionString(rawConn);

        services.AddDbContext<GuardianDbContext>(options =>
        {
            if (IsPostgresConnection(formattedConn))
            {
                options.UseNpgsql(formattedConn, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
            }
            else
            {
                options.UseSqlite(formattedConn);
            }
        });

        services.AddScoped<IGuardianDbContext>(provider => provider.GetRequiredService<GuardianDbContext>());

        // 2. Register Agent Audit Log Service
        services.AddSingleton<IAgentLogService, AgentLogService>();

        // 3. Register LLM Symptom Interpretation Service
        services.AddSingleton<ILanguageModelService, LanguageModelService>();

        // 4. Register Preliminary Medical Evaluation Agent Service
        services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();

        // 5. Register Emergency Management Agent Service
        services.AddScoped<IEmergencyManagementService, EmergencyManagementService>();

        // 6. Register Persistent Memory & Learning Agent Service
        services.AddScoped<ILearningStatsService, LearningStatsService>();

        // 7. Register Background Telemetry & Event Analysis Services
        services.AddHostedService<SensorSimulatorService>();
        services.AddHostedService<EventAnalysisService>();

        return services;
    }

    /// <summary>
    /// Checks if a connection string target is PostgreSQL.
    /// </summary>
    public static bool IsPostgresConnection(string conn)
    {
        if (string.IsNullOrWhiteSpace(conn)) return false;
        var lower = conn.ToLowerInvariant();
        return lower.Contains("host=") ||
               lower.Contains("server=") ||
               lower.Contains("postgres://") ||
               lower.Contains("postgresql://") ||
               lower.Contains("user id=") ||
               lower.Contains("username=");
    }

    /// <summary>
    /// Parses standard URIs (postgres://user:password@host:port/database) from Render into valid ADO.NET connection strings.
    /// </summary>
    public static string FormatConnectionString(string rawConn)
    {
        if (string.IsNullOrWhiteSpace(rawConn)) return rawConn;

        if (rawConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            rawConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(rawConn);
                var userInfo = uri.UserInfo.Split(':', 2);
                var username = userInfo[0];
                var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
            }
            catch
            {
                return rawConn;
            }
        }

        return rawConn;
    }
}
