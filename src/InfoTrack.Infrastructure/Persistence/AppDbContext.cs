using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace InfoTrack.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    internal DbSet<SearchRunEntity> SearchRuns => Set<SearchRunEntity>();
    internal DbSet<LocationOutcomeEntity> LocationOutcomes => Set<LocationOutcomeEntity>();
    internal DbSet<FirmEntity> Firms => Set<FirmEntity>();
    internal DbSet<SightingEntity> Sightings => Set<SightingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}

internal sealed class SearchRunConfiguration : IEntityTypeConfiguration<SearchRunEntity>
{
    public void Configure(EntityTypeBuilder<SearchRunEntity> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.RunAtUtc);

        // Stored as JSON text so the same schema works for both Postgres (runtime) and SQLite (tests).
        var jsonConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var jsonComparer = new ValueComparer<List<string>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        builder.Property(r => r.RequestedLocations)
            .HasConversion(jsonConverter, jsonComparer);

        builder.HasMany(r => r.Locations)
            .WithOne(l => l.SearchRun)
            .HasForeignKey(l => l.SearchRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LocationOutcomeConfiguration : IEntityTypeConfiguration<LocationOutcomeEntity>
{
    public void Configure(EntityTypeBuilder<LocationOutcomeEntity> builder)
    {
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Status).HasConversion<string>();

        builder.HasMany(l => l.Sightings)
            .WithOne(s => s.LocationOutcome)
            .HasForeignKey(s => s.LocationOutcomeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FirmConfiguration : IEntityTypeConfiguration<FirmEntity>
{
    public void Configure(EntityTypeBuilder<FirmEntity> builder)
    {
        builder.HasKey(f => f.Id);
        builder.HasIndex(f => f.IdentityKey).IsUnique();

        // Firms are shared across runs — never cascade-delete from a sighting.
        builder.HasMany(f => f.Sightings)
            .WithOne(s => s.Firm)
            .HasForeignKey(s => s.FirmId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SightingConfiguration : IEntityTypeConfiguration<SightingEntity>
{
    public void Configure(EntityTypeBuilder<SightingEntity> builder)
    {
        builder.HasKey(s => s.Id);
    }
}
