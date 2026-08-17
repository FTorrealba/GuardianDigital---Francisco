using GuardianDigital.Application;
using GuardianDigital.Infrastructure;
using GuardianDigital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// CORS setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Auto-migrate EF Core DB on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GuardianDbContext>();
    db.Database.Migrate();
}

app.UseCors("AllowFrontend");

// Map Feature Endpoints (Vertical Slice Architecture)
app.MapFeatureEndpoints();

app.Run();
