using Meridian.Domain.Entities;
using Meridian.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Meridian.Infrastructure.Persistence;

// IdentityDbContext<AppUser> — by inheriting from this instead of plain DbContext, we get
public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Immigration data
    public DbSet<Judge> Judges => Set<Judge>();
    public DbSet<CourtCase> CourtCases => Set<CourtCase>();
    public DbSet<DetentionFacility> DetentionFacilities => Set<DetentionFacility>();
    public DbSet<DetentionPopulationSnapshot> DetentionSnapshots => Set<DetentionPopulationSnapshot>();
    public DbSet<RemovalStatistic> RemovalStatistics => Set<RemovalStatistic>();
    public DbSet<EnforcementAction> EnforcementActions => Set<EnforcementAction>();

    // SaaS / Alerting
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<AlertNotification> AlertNotifications => Set<AlertNotification>();

    /// <summary>
    /// Tells EF Core exactly how to map each entity to a table. Every index we define here becomes a real
    /// PostgreSQL index — critical for query performance when you're dealing with hundreds of thousands of court cases.
    /// </summary>
    /// <param name="builder"></param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>().ToTable("users");

        // ── Judge ─────────────────────────────────────────────────────────────
        builder.Entity<Judge>(e =>
        {
            e.ToTable("judges");
            e.HasKey(x => x.Id);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.CourtCity).HasMaxLength(100).IsRequired();
            e.Property(x => x.CourtState).HasMaxLength(2).IsRequired();
            e.Property(x => x.CourtCode).HasMaxLength(20);
            e.Property(x => x.GrantRatePercent).HasPrecision(5, 2);
            e.Property(x => x.DenialRatePercent).HasPrecision(5, 2);
            e.Property(x => x.RemovalOrderRatePercent).HasPrecision(5, 2);
            e.HasIndex(x => new { x.CourtCity, x.CourtState });
            e.HasIndex(x => new { x.LastName, x.FirstName });
        });

        // ── CourtCase ─────────────────────────────────────────────────────────
        builder.Entity<CourtCase>(e =>
        {
            e.ToTable("court_cases");
            e.HasKey(x => x.Id);
            e.Property(x => x.CaseNumber).HasMaxLength(50).IsRequired();
            e.Property(x => x.CourtCity).HasMaxLength(100).IsRequired();
            e.Property(x => x.CourtState).HasMaxLength(2).IsRequired();
            e.Property(x => x.RespondentNationality).HasMaxLength(3);
            e.Property(x => x.SourceRecordId).HasMaxLength(100);
            e.HasOne(x => x.Judge)
                .WithMany(j => j.Cases)
                .HasForeignKey(x => x.JudgeId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.CaseNumber).IsUnique();
            e.HasIndex(x => x.JudgeId);
            e.HasIndex(x => new { x.CourtCity, x.CourtState });
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.RespondentNationality);
            e.HasIndex(x => x.FilingDate);
        });

        // ── DetentionFacility ─────────────────────────────────────────────────
        builder.Entity<DetentionFacility>(e =>
        {
            e.ToTable("detention_facilities");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.AffNumber).HasMaxLength(20);
            e.Property(x => x.City).HasMaxLength(100).IsRequired();
            e.Property(x => x.State).HasMaxLength(2).IsRequired();
            e.HasIndex(x => x.AffNumber);
            e.HasIndex(x => x.State);
        });

        builder.Entity<DetentionPopulationSnapshot>(e =>
        {
            e.ToTable("detention_population_snapshots");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Facility)
                .WithMany(f => f.PopulationSnapshots)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.FacilityId, x.SnapshotDate });
        });

        // ── RemovalStatistic ──────────────────────────────────────────────────
        builder.Entity<RemovalStatistic>(e =>
        {
            e.ToTable("removal_statistics");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nationality).HasMaxLength(10).IsRequired();
            e.Property(x => x.NationalityDisplay).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.Year, x.Month, x.NationalityDisplay }).IsUnique();
            e.HasIndex(x => x.Nationality);
            e.HasIndex(x => new { x.Year, x.Month });
        });

        // ── EnforcementAction ─────────────────────────────────────────────────
        builder.Entity<EnforcementAction>(e =>
        {
            e.ToTable("enforcement_actions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nationality).HasMaxLength(100);
            e.Property(x => x.SourceRecordId).HasMaxLength(100);
            e.HasIndex(x => x.ActionDate);
            e.HasIndex(x => x.Nationality);
            e.HasIndex(x => x.ActionType);
            e.HasIndex(x => x.SourceRecordId);
            e.Property(x => x.DispositionCode).HasMaxLength(50);
        });

        // ── Tenant ────────────────────────────────────────────────────────────
        builder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(100).IsRequired();
            e.Property(x => x.StripeCustomerId).HasMaxLength(100);
            e.Property(x => x.StripeSubscriptionId).HasMaxLength(100);
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.StripeCustomerId);
        });

        // ── AlertRule ─────────────────────────────────────────────────────────
        builder.Entity<AlertRule>(e =>
        {
            e.ToTable("alert_rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Threshold).HasPrecision(10, 2);
            e.HasIndex(x => x.TenantId);
        });

        builder.Entity<AlertNotification>(e =>
        {
            e.ToTable("alert_notifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasOne(x => x.AlertRule)
                .WithMany(r => r.Notifications)
                .HasForeignKey(x => x.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.IsRead });
        });
    }

    /// <summary>
    /// SetUpdated() gets called automatically. Any time EF Core saves a modified entity, the UpdatedAt timestamp is
    /// set without us having to remember to do it manually anywhere.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modified = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .OfType<Domain.Common.BaseEntity>();

        foreach (var entity in modified)
            entity.SetUpdated();

        return base.SaveChangesAsync(cancellationToken);
    }
}