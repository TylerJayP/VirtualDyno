// Services/DynoCalculationService.cs - BALANCED VERSION
using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public class DynoCalculationService : IDynoCalculationService
    {
        public double CalculateAdvancedHorsepower(AdvancedDynoData data, int gear, CarPreset carPreset,
            AdvancedDynoSettings? settings = null)
        {
            settings ??= new AdvancedDynoSettings();

            // Adjusted base calculation to hit 237 WHP target
            // Current result: 110.5 WHP, Target: 237 WHP
            // Need to increase by factor of ~2.15
            double baseHP = data.MassAirflow * 1.1; // Increased from 0.75
            baseHP *= Math.Min(data.Load * 1.2, 1.0); // Increased load factor

            // Small displacement adjustment
            var displacementFactor = 1.0 + ((carPreset.Displacement - 2.0) * 0.03);
            baseHP *= displacementFactor;

            // Apply corrections conservatively
            if (settings.UseAFRCorrection && data.AFR > 0)
            {
                var afrCorrection = CalculateAFRCorrection(data.AFR, data.IsForceInduction);
                baseHP *= afrCorrection;
            }

            if (settings.UseKnockCorrection && data.KnockRetard > 0)
            {
                var knockCorrection = 1.0 - (data.KnockRetard * 0.02); // Slightly increased penalty
                baseHP *= knockCorrection;
            }

            // Only apply boost correction for efficiency losses
            if (settings.UseBoostCorrection && data.BoostPressure > 0)
            {
                var boostEfficiencyLoss = CalculateBoostEfficiencyLoss(data.BoostPressure, data.IntakeAirTemp);
                baseHP *= boostEfficiencyLoss;
            }

            if (settings.UseAtmosphericCorrection && data.IntakeAirTemp > 0)
            {
                var tempCorrection = CalculateTemperatureCorrection(data.IntakeAirTemp);
                baseHP *= tempCorrection;
            }

            // Simplified VE correction
            if (settings.UseVolumetricEfficiency)
            {
                var veCorrection = CalculateTurboVE(data.Rpm, data.BoostPressure);
                baseHP *= veCorrection;
            }

            // Apply gear-specific corrections
            var gearCorrection = gear switch
            {
                3 => 0.97, // 3rd gear: slightly lower
                4 => 1.0,  // 4th gear: standard
                5 => 1.02, // 5th gear: slightly higher
                _ => 1.0
            };
            baseHP *= gearCorrection;

            // Apply drivetrain type correction
            var drivetrainCorrection = carPreset.DriveType switch
            {
                "FWD" => 1.0,    // Front-wheel drive baseline
                "AWD" => 0.96,   // All-wheel drive has more loss
                "RWD" => 0.98,   // Rear-wheel drive
                _ => 1.0
            };
            baseHP *= drivetrainCorrection;

            // Balanced calibration factors to hit realistic numbers
            var calibrationFactor = settings.CalibrationOverride ?? GetBalancedCalibrationFactor(carPreset.Key);
            baseHP *= calibrationFactor;

            return Math.Max(baseHP, 0);
        }

        private double CalculateAFRCorrection(double afr, bool isForceInduction)
        {
            // Conservative AFR correction
            double optimalAFR = isForceInduction ? 12.0 : 12.5;

            if (afr < 10.0 || afr > 18.0) return 0.95; // Small penalty for bad data

            double afrDeviation = Math.Abs(afr - optimalAFR);

            if (afrDeviation <= 1.0)
                return 1.0; // Within optimal range
            else if (afrDeviation <= 2.0)
                return 1.0 - (afrDeviation - 1.0) * 0.03; // -3% per AFR point
            else
                return 1.0 - 0.03 - (afrDeviation - 2.0) * 0.04; // Steeper penalty beyond 2.0
        }

        private double CalculateBoostEfficiencyLoss(double boostPressure, double intakeTemp)
        {
            // Account for efficiency losses only
            double efficiencyLoss = 1.0;

            // Heat soak penalty (hot air reduces efficiency)
            if (intakeTemp > 100)
            {
                efficiencyLoss -= (intakeTemp - 100) * 0.003; // Small penalty for heat
            }

            // Very high boost can reduce efficiency due to pumping losses
            if (boostPressure > 15)
            {
                efficiencyLoss -= (boostPressure - 15) * 0.008; // Penalty for excessive boost
            }

            return Math.Max(efficiencyLoss, 0.88); // Minimum 12% loss cap
        }

        private double CalculateTemperatureCorrection(double intakeTemp)
        {
            if (intakeTemp <= 0) return 1.0; // No data

            // Conservative temperature correction based on air density
            double tempCorrection = Math.Sqrt(537.67 / (intakeTemp + 459.67));

            // Limit correction to reasonable bounds
            return Math.Max(Math.Min(tempCorrection, 1.08), 0.92); // Max ±8% correction
        }


        private double CalculateTurboVE(int rpm, double boostPressure)
        {
            double baseVE = rpm switch
            {
                < 2500 => 0.95,   // Low RPM before turbo spools
                >= 2500 and < 3000 => 0.95 + (rpm - 2500) * 0.0002, // Building boost
                >= 3000 and < 4500 => 1.08, // ← BOOSTED: Peak torque zone
                >= 4500 and < 5500 => 1.05, // Strong mid-range
                >= 5500 and < 6000 => 1.02, // Approaching peak HP  
                >= 6000 => 1.0    // Peak HP zone (unchanged)
            };

            // Plus small boost pressure effect when under boost
            if (boostPressure > 5)
            {
                double boostEffect = 1.0 + Math.Min((boostPressure - 5) * 0.008, 0.12);
                baseVE *= boostEffect;
            }

            return baseVE;
        }

        private double GetBalancedCalibrationFactor(string carKey)
        {
            // Calibration factors adjusted to hit realistic targets
            // Mazdaspeed3 target: ~237 WHP
            // Current result was 110.5 WHP, so need ~2.15x increase
            return carKey switch
            {
                "mazdaspeed3" => 0.90, // Increased significantly to hit 237 WHP target
                "wrx" => 1.40,         // Adjusted proportionally
                "sti" => 1.35,         // STI has more drivetrain loss
                "evo" => 1.38,         // Evo AWD system
                "gti" => 1.48,         // Modern efficient drivetrain
                "focus_st" => 1.46,    // Ford ST series
                _ => 1.40              // Default
            };
        }

        public double CalculateTorque(double horsepower, int rpm)
        {
            return rpm > 0 ? (horsepower * 5252) / rpm : 0;
        }

        public List<DynoDataPoint> SmoothData(List<DynoDataPoint> data, int smoothingLevel)
        {
            if (smoothingLevel <= 1 || !data.Any()) return data;

            var smoothedData = new List<DynoDataPoint>();
            var windowSize = smoothingLevel;

            for (int i = 0; i < data.Count; i++)
            {
                var start = Math.Max(0, i - windowSize);
                var end = Math.Min(data.Count - 1, i + windowSize);
                var window = data.Skip(start).Take(end - start + 1).ToList();

                var smoothedPoint = new DynoDataPoint
                {
                    DynoRunId = data[i].DynoRunId,
                    Rpm = data[i].Rpm,
                    Horsepower = Math.Round(window.Average(p => p.Horsepower), 1),
                    Torque = Math.Round(window.Average(p => p.Torque), 1),
                    Boost = Math.Round(window.Average(p => p.Boost), 1),
                    MassAirflow = data[i].MassAirflow,
                    Load = data[i].Load,
                    Timestamp = data[i].Timestamp
                };

                smoothedData.Add(smoothedPoint);
            }

            return smoothedData;
        }

        public PeakValues CalculatePeakValues(List<DynoDataPoint> data, int carWeight)
        {
            if (!data.Any()) return new PeakValues();

            var maxHpPoint = data.OrderByDescending(d => d.Horsepower).First();
            var maxTqPoint = data.OrderByDescending(d => d.Torque).First();
            var maxBoost = data.Max(d => d.Boost);

            var powerToWeightRatio = maxHpPoint.Horsepower / (carWeight / 1000.0);

            return new PeakValues
            {
                MaxHorsepower = Math.Round(maxHpPoint.Horsepower, 1),
                MaxHorsepowerRpm = maxHpPoint.Rpm,
                MaxTorque = Math.Round(maxTqPoint.Torque, 1),
                MaxTorqueRpm = maxTqPoint.Rpm,
                MaxBoost = Math.Round(maxBoost, 1),
                PowerToWeightRatio = Math.Round(powerToWeightRatio, 1)
            };
        }
    }
}