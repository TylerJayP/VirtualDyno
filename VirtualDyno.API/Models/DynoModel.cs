using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualDyno.API.Models
{
    public class DynoRun
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string FileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public CarConfiguration Car { get; set; } = new();

        public List<DynoDataPoint> DataPoints { get; set; } = new();

        public PeakValues? Peaks { get; set; }

        [Range(0, 5)]  // Changed from [Range(1, 5)]
        public int SmoothingLevel { get; set; } = 1;

        [Range(3, 6)]
        public int GearUsed { get; set; } = 4;

        [MaxLength(50)]
        public string CalculationMethod { get; set; } = string.Empty; // ← ADD THIS LINE

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

        public bool IsPublic { get; set; } = false;
    }

    public class DynoDataPoint
    {
        public int Id { get; set; }

        public int DynoRunId { get; set; }
        public DynoRun DynoRun { get; set; } = null!;

        [Range(0, 10000)]
        public int Rpm { get; set; }

        [Range(0, 2000)]
        public double Horsepower { get; set; }

        [Range(0, 2000)]
        public double Torque { get; set; }

        [Range(-20, 50)]
        public double Boost { get; set; }

        [Range(0, 1000)]
        public double MassAirflow { get; set; }

        [Range(0, 1)]
        public double Load { get; set; }

        public DateTime Timestamp { get; set; }
    }

    public class PeakValues
    {
        public int Id { get; set; }

        public int DynoRunId { get; set; }
        public DynoRun DynoRun { get; set; } = null!;

        [Range(0, 2000)]
        public double MaxHorsepower { get; set; }

        [Range(0, 10000)]
        public int MaxHorsepowerRpm { get; set; }

        [Range(0, 2000)]
        public double MaxTorque { get; set; }

        [Range(0, 10000)]
        public int MaxTorqueRpm { get; set; }

        [Range(-20, 50)]
        public double MaxBoost { get; set; }

        [Range(0, 500)]
        public double PowerToWeightRatio { get; set; }
    }

    public class CarConfiguration
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(1000, 10000)]
        public int Weight { get; set; }

        [Range(0.5, 8.0)]
        public double Displacement { get; set; }

        [MaxLength(20)]
        public string DriveType { get; set; } = "FWD";

        [MaxLength(50)]
        public string PresetKey { get; set; } = string.Empty;
    }

    public class CarPreset
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(1000, 10000)]
        public int Weight { get; set; }

        [Range(0.5, 8.0)]
        public double Displacement { get; set; }

        [MaxLength(20)]
        public string DriveType { get; set; } = "FWD";

        [Column(TypeName = "decimal(3,2)")]
        public double GearRatio3rd { get; set; }

        [Column(TypeName = "decimal(3,2)")]
        public double GearRatio4th { get; set; }

        [Column(TypeName = "decimal(3,2)")]
        public double GearRatio5th { get; set; }

        [Column(TypeName = "decimal(4,3)")]
        public double FinalDrive { get; set; }

        public bool IsActive { get; set; } = true;
    }
}