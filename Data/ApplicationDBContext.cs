using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VirtualDyno.API.Models;

namespace VirtualDyno.API.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<DynoRun> DynoRuns { get; set; }
        public DbSet<DynoDataPoint> DynoDataPoints { get; set; }
        public DbSet<PeakValues> PeakValues { get; set; }
        public DbSet<CarConfiguration> CarConfigurations { get; set; }
        public DbSet<CarPreset> CarPresets { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<DynoRun>()
                .HasMany(d => d.DataPoints)
                .WithOne(p => p.DynoRun)
                .HasForeignKey(p => p.DynoRunId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DynoRun>()
                .HasOne(d => d.Peaks)
                .WithOne(p => p.DynoRun)
                .HasForeignKey<PeakValues>(p => p.DynoRunId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<DynoRun>()
                .HasOne(d => d.Car)
                .WithMany()
                .HasForeignKey("CarConfigurationId")
                .OnDelete(DeleteBehavior.Restrict);

            // Indexes for performance
            builder.Entity<DynoRun>()
                .HasIndex(d => d.UserId);

            builder.Entity<DynoRun>()
                .HasIndex(d => d.CreatedAt);

            builder.Entity<DynoDataPoint>()
                .HasIndex(d => new { d.DynoRunId, d.Rpm });

            // Seed car presets
            builder.Entity<CarPreset>().HasData(
                new CarPreset
                {
                    Id = 1,
                    Key = "mazdaspeed3",
                    Name = "Mazdaspeed3",
                    Weight = 3200,
                    Displacement = 2.3,
                    DriveType = "FWD",
                    GearRatio3rd = 2.28,
                    GearRatio4th = 1.65,
                    GearRatio5th = 1.28,
                    FinalDrive = 4.438
                },
                new CarPreset
                {
                    Id = 2,
                    Key = "wrx",
                    Name = "Subaru WRX",
                    Weight = 3267,
                    Displacement = 2.0,
                    DriveType = "AWD",
                    GearRatio3rd = 2.37,
                    GearRatio4th = 1.72,
                    GearRatio5th = 1.34,
                    FinalDrive = 4.11
                },
                new CarPreset
                {
                    Id = 3,
                    Key = "sti",
                    Name = "Subaru STI",
                    Weight = 3391,
                    Displacement = 2.5,
                    DriveType = "AWD",
                    GearRatio3rd = 2.37,
                    GearRatio4th = 1.72,
                    GearRatio5th = 1.34,
                    FinalDrive = 4.11
                },
                new CarPreset
                {
                    Id = 4,
                    Key = "evo",
                    Name = "Mitsubishi Evo",
                    Weight = 3263,
                    Displacement = 2.0,
                    DriveType = "AWD",
                    GearRatio3rd = 2.27,
                    GearRatio4th = 1.65,
                    GearRatio5th = 1.28,
                    FinalDrive = 4.53
                },
                new CarPreset
                {
                    Id = 5,
                    Key = "gti",
                    Name = "VW Golf GTI",
                    Weight = 3126,
                    Displacement = 2.0,
                    DriveType = "FWD",
                    GearRatio3rd = 2.13,
                    GearRatio4th = 1.45,
                    GearRatio5th = 1.13,
                    FinalDrive = 3.65
                },
                new CarPreset
                {
                    Id = 6,
                    Key = "focus_st",
                    Name = "Ford Focus ST",
                    Weight = 3223,
                    Displacement = 2.0,
                    DriveType = "FWD",
                    GearRatio3rd = 2.13,
                    GearRatio4th = 1.45,
                    GearRatio5th = 1.13,
                    FinalDrive = 3.82
                }
            );
        }
    }
}