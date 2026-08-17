using GuardianDigital.Application;
using GuardianDigital.Infrastructure;
using GuardianDigital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Port Configuration for Render & Container Environments
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 2. Add services (Infrastructure & Application vertical slices)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// 3. CORS setup for Localhost, Vercel, Netlify and custom production origins
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
{
    "http://localhost:5173",
    "http://127.0.0.1:5173",
    "http://localhost:3000",
    "http://localhost:4173",
    "https://guardian-digital.vercel.app",
    "https://guardian-digital.netlify.app"
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(configuredOrigins)
              .SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrWhiteSpace(origin)) return false;
                  if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                  {
                      return uri.Host == "localhost" ||
                             uri.Host == "127.0.0.1" ||
                             uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase) ||
                             uri.Host.EndsWith(".netlify.app", StringComparison.OrdinalIgnoreCase) ||
                             configuredOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
                  }
                  return false;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// 4. Database Schema Setup on Startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GuardianDbContext>();
    
    if (db.Database.IsSqlite())
    {
        db.Database.Migrate();

        // Clean up legacy test incidents with English labels on SQLite
        var legacyIncidents = db.Incidents
            .Include(i => i.ActionsExecuted)
            .Include(i => i.UserResponses)
            .Where(i => i.OriginalDescription.Contains("Reported:") ||
                        i.OriginalDescription.Contains("Symptoms:") ||
                        i.OriginalDescription.Contains("CRITICAL FALL") ||
                        i.OriginalDescription.Contains("CARDIAC ALERT") ||
                        i.OriginalDescription.Contains("IMMOBILITY ALERT") ||
                        i.OriginalDescription.Contains("PERIMETER SECURITY") ||
                        i.OriginalDescription.Contains("RESPIRATORY ALERT") ||
                        i.OriginalDescription.Contains("I have noted"))
            .ToList();

        if (legacyIncidents.Any())
        {
            db.Incidents.RemoveRange(legacyIncidents);
            db.SaveChanges();
        }
    }
    else
    {
        // On PostgreSQL (e.g. Render Managed Postgres), create database schema matching domain model
        db.Database.EnsureCreated();
    }
}

app.UseCors("AllowFrontend");

// 5. Map Feature Endpoints (Vertical Slice Architecture)
app.MapFeatureEndpoints();

app.Run();
