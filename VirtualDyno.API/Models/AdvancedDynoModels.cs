using System.ComponentModel.DataAnnotations;

namespace VirtualDyno.API.Models
{
    /// <summary>
    /// Extended data model for advanced dyno calculations
    /// Contains additional parameters from datalog files
    /// </summary>
    public class AdvancedDynoData
    {
        [Range(0, 10000)]
        public int Rpm { get; set; }

        [Range(8.0, 20.0)]
        public double AFR { get; set; } = 0; // Air-Fuel Ratio (0 = not available)

        [Range(-20, 50)]
        public double BoostPressure { get; set; } // PSI

        [Range(-40, 200)]
        public double IntakeAirTemp { get; set; } = 0; // Fahrenheit (0 = not available)

        [Range(0, 30)]
        public double KnockRetard { get; set; } = 0; // Degrees

        [Range(25.0, 35.0)]
        public double BarometricPressure { get; set; } = 0; // InHg (0 = not available)

        [Range(0, 1.0)]
        public double Humidity { get; set; } = 0; // 0 = not available

        public bool IsForceInduction { get; set; } = true; // Turbo/Supercharged

        [Range(0, 100)]
        public double ThrottlePosition { get; set; } = 0; // Percentage

        [Range(-10, 50)]
        public double IgnitionTiming { get; set; } = 0; // Degrees BTDC

        [Range(0, 1000)]
        public double MassAirflow { get; set; } // g/s - REQUIRED

        [Range(0, 1)]
        public double Load { get; set; } // Engine load percentage - REQUIRED
    }

    /// <summary>
    /// Configuration for advanced dyno calculations
    /// </summary>
    public class AdvancedDynoSettings
    {
        public bool UseAtmosphericCorrection { get; set; } = true;
        public bool UseAFRCorrection { get; set; } = true;
        public bool UseKnockCorrection { get; set; } = true;
        public bool UseBoostCorrection { get; set; } = true;
        public bool UseVolumetricEfficiency { get; set; } = true;

        /// <summary>
        /// Override calibration factor for specific car
        /// </summary>
        public double? CalibrationOverride { get; set; }

        /// <summary>
        /// Local altitude in feet (affects atmospheric pressure)
        /// </summary>
        public double Altitude { get; set; } = 0;
    }
}