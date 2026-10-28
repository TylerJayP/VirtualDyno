using Microsoft.EntityFrameworkCore;

namespace VirtualDyno.API.Data;

public class DynoDbContext(DbContextOptions<DynoDbContext> options) : DbContext(options)
{
    public DbSet<DynoRunEntity> Runs => Set<DynoRunEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var run = modelBuilder.Entity<DynoRunEntity>();
        run.ToTable("DynoRuns");
        run.Property(r => r.Name).HasMaxLength(200).IsRequired();
        run.Property(r => r.FileName).HasMaxLength(260).IsRequired();
        run.Property(r => r.Notes).HasMaxLength(2000);
        run.Property(r => r.LogText).IsRequired();
        run.Property(r => r.SettingsJson).IsRequired();
        run.Property(r => r.VehicleKey).HasMaxLength(64);
        run.Property(r => r.VehicleName).HasMaxLength(200).IsRequired();
        run.Property(r => r.Correction).HasMaxLength(20).IsRequired();
        run.HasIndex(r => r.CreatedAtUtc);
    }
}
