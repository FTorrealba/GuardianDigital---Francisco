using System.Text.Json;
using GuardianDigital.Application.Common.Interfaces;
using GuardianDigital.Domain.Entities;
using GuardianDigital.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GuardianDigital.Infrastructure.Persistence;

public class GuardianDbContext : DbContext, IGuardianDbContext
{
    public GuardianDbContext(DbContextOptions<GuardianDbContext> options) : base(options)
    {
    }

    public DbSet<SystemHealth> SystemHealthChecks => Set<SystemHealth>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MedicalProfile> MedicalProfiles => Set<MedicalProfile>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<LinkedDevice> LinkedDevices => Set<LinkedDevice>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<UserResponse> UserResponses => Set<UserResponse>();
    public DbSet<ActionExecuted> ActionsExecuted => Set<ActionExecuted>();
    public DbSet<SensorReading> SensorReadings => Set<SensorReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SystemHealth>(builder =>
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        });

        // User Configuration
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            builder.Property(u => u.NationalId).IsRequired().HasMaxLength(50);
            builder.Property(u => u.PrimaryPhone).IsRequired().HasMaxLength(30);

            // Value Object conversion for BloodType
            builder.Property(u => u.BloodType)
                .HasConversion(
                    v => v.Value,
                    v => new BloodType(v))
                .HasMaxLength(10)
                .IsRequired();

            var navigation = builder.Metadata.FindNavigation(nameof(User.EmergencyContacts));
            navigation?.SetPropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(u => u.EmergencyContacts)
                .WithOne(c => c.User)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(u => u.MedicalProfile)
                .WithOne(m => m.User)
                .HasForeignKey<MedicalProfile>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.LinkedDevices)
                .WithOne(d => d.User)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(u => u.Incidents)
                .WithOne(i => i.User)
                .HasForeignKey(i => i.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // MedicalProfile Configuration
        modelBuilder.Entity<MedicalProfile>(builder =>
        {
            builder.HasKey(m => m.Id);

            var jsonOptions = new JsonSerializerOptions();
            var stringListComparer = new ValueComparer<List<string>>(
                (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            builder.Property(m => m.CurrentMedication)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            builder.Property(m => m.KnownAllergies)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);

            builder.Property(m => m.PreexistingConditions)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, jsonOptions),
                    v => JsonSerializer.Deserialize<List<string>>(v, jsonOptions) ?? new List<string>())
                .Metadata.SetValueComparer(stringListComparer);
        });

        // EmergencyContact Configuration
        modelBuilder.Entity<EmergencyContact>(builder =>
        {
            builder.HasKey(c => c.Id);
            builder.Property(c => c.ContactName).IsRequired().HasMaxLength(150);
            builder.Property(c => c.Phone).IsRequired().HasMaxLength(30);
            builder.Property(c => c.Relationship).HasMaxLength(50);
        });

        // LinkedDevice Configuration
        modelBuilder.Entity<LinkedDevice>(builder =>
        {
            builder.HasKey(d => d.Id);

            builder.HasMany(d => d.Readings)
                .WithOne(r => r.Device)
                .HasForeignKey(r => r.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Incident Configuration
        modelBuilder.Entity<Incident>(builder =>
        {
            builder.HasKey(i => i.Id);

            builder.HasMany(i => i.UserResponses)
                .WithOne(r => r.Incident)
                .HasForeignKey(r => r.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(i => i.ActionsExecuted)
                .WithOne(a => a.Incident)
                .HasForeignKey(a => a.IncidentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserResponse Configuration
        modelBuilder.Entity<UserResponse>(builder =>
        {
            builder.HasKey(r => r.Id);
        });

        // ActionExecuted Configuration
        modelBuilder.Entity<ActionExecuted>(builder =>
        {
            builder.HasKey(a => a.Id);
        });

        // SensorReading Configuration
        modelBuilder.Entity<SensorReading>(builder =>
        {
            builder.HasKey(r => r.Id);
        });
    }
}
