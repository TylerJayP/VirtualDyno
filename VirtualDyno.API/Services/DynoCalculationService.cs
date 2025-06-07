using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public class DynoCalculationService : IDynoCalculationService
    {
        public double CalculateAdvancedHorsepower(AdvancedDynoData data, int gear, CarPreset carPreset,
            AdvancedDynoSettings? settings = null)
        {
            settings ??= new AdvancedDynoSettings();

            // MULTI-METHOD APPROACH: Handle different sensor configurations
            // Priority: MAF > MAP > Load-based calculation

            double baseHP;
            string calculationMethod;

            if (data.MassAirflow > 5) // MAF sensor available and reading valid data
            {
                // Method 1: MAF-based calculation (most accurate)
                baseHP = CalculateFromMAF(data, carPreset);
                calculationMethod = "MAF";
            }
            else if (data.BoostPressure > -10 && data.IntakeAirTemp > 0) // MAP sensor data available
            {
                // Method 2: MAP-based calculation (for speed density tunes)
                baseHP = CalculateFromMAP(data, carPreset);
                calculationMethod = "MAP";
            }
            else
            {
                // Method 3: Load-based calculation (universal fallback)
                baseHP = CalculateFromLoad(data, carPreset);
                calculationMethod = "Load";
            }

            // Apply common corrections regardless of calculation method
            if (settings.UseAFRCorrection && data.AFR > 0)
            {
                var afrCorrection = CalculateSimpleAFRCorrection(data.AFR, data.IsForceInduction);
                baseHP *= afrCorrection;
            }

            if (settings.UseKnockCorrection && data.KnockRetard > 0)
            {
                var knockCorrection = 1.0 - (data.KnockRetard * 0.018);
                baseHP *= knockCorrection;
            }

            if (settings.UseAtmosphericCorrection && data.IntakeAirTemp > 0)
            {
                var atmCorrection = CalculateSimpleAtmosphericCorrection(data.IntakeAirTemp);
                baseHP *= atmCorrection;
            }

            if (settings.UseVolumetricEfficiency)
            {
                var veCorrection = CalculateSimpleVE(data.Rpm);
                baseHP *= veCorrection;
            }

            // Gear and drivetrain corrections
            var gearCorrection = gear switch
            {
                3 => 0.975,
                4 => 1.0,
                5 => 1.015,
                _ => 1.0
            };
            baseHP *= gearCorrection;

            var drivetrainCorrection = carPreset.DriveType switch
            {
                "FWD" => 1.0, // 230hp FWD car baseline --> 230hp
                "AWD" => 0.955, // 230hp FWD car baseline --> 219.65 --> 5%
                "RWD" => 0.98, // 230hp FWD car baseline --> 225hp --> 2%
                _ => 1.0
            };
            baseHP *= drivetrainCorrection;

            // Method-specific calibration factors
            var calibrationFactor = settings.CalibrationOverride ?? GetCalibrationFactor(carPreset.Key, calculationMethod);
            baseHP *= calibrationFactor;

            return Math.Max(baseHP, 0);
        }

        public double CalculateGearCorrection(int gear, CarPreset carPreset)
        {
            var gearRatio = gear switch
            {
                3 => carPreset.GearRatio3rd,
                4 => carPreset.GearRatio4th, // Fourth gear is usually direct drive
                5 => carPreset.GearRatio5th, // Fifth gear may have slight overdrive
                _ => carPreset.GearRatio4th
            };

            var baselineRatio = carPreset.GearRatio4th;

            // Higher the gear ratio, the more torque multiplication -> Slighly lower dyno reading
            // This is a simplification, but it works for most cases
            var ratioCorrection = baselineRatio / gearRatio;

            return 0.98 + (ratioCorrection * 0.015);
        }

        public double CalculateFromMAF(AdvancedDynoData data, CarPreset carPreset)
        {
            // Method 1: MAF-based (most accurate)
            double baseHP = data.MassAirflow * 1.08;

            var loadFactor = Math.Min(data.Load * 1.15, 1.05);
            baseHP *= loadFactor;

            var displacementFactor = 1.0 + ((carPreset.Displacement - 2.0) * 0.025);
            baseHP *= displacementFactor;

            return baseHP;
        }

        public double CalculateFromMAP(AdvancedDynoData data, CarPreset carPreset)
        {
            // Method 2: MAP-based calculation for speed density tunes
            // Calculate theoretical mass airflow from MAP, RPM, and displacement

            // Convert boost to absolute pressure
            double absolutePressure = Math.Abs(data.BoostPressure) + 14.7; // psia
            double atmosphericPressure = 14.7; // psia

            // Pressure ratio (how much denser the air is vs atmospheric)
            double pressureRatio = absolutePressure / atmosphericPressure;

            // Temperature correction (how much less dense due to heat)
            double standardTemp = 77; // °F (25°C standard)
            double actualTemp = data.IntakeAirTemp > 0 ? data.IntakeAirTemp : 100;
            double temperatureRatio = (standardTemp + 459.67) / (actualTemp + 459.67);

            // Combined density ratio
            double densityRatio = pressureRatio * temperatureRatio;

            // Calculate theoretical airflow for naturally aspirated engine at standard conditions
            // Formula: Base airflow (g/s) ≈ Displacement(L) × RPM × VE × 0.0135
            double baseVE = data.Rpm switch
            {
                < 2500 => 0.75,
                >= 2500 and < 3500 => 0.85,
                >= 3500 and < 4500 => 0.95, // Peak efficiency
                >= 4500 and < 5500 => 0.90,
                _ => 0.85 // >= 5500
            };

            double theoreticalAirflow = carPreset.Displacement * data.Rpm * baseVE * 0.0135;

            // Apply density correction for boost and temperature
            double actualAirflow = theoreticalAirflow * densityRatio;

            // Apply load factor (accounts for throttle position)
            var loadFactor = Math.Min(data.Load * 1.2, 1.05);

            // Convert airflow to horsepower (same as MAF method)
            double baseHP = actualAirflow * 1.08 * loadFactor;

            return baseHP;
        }

        public double CalculateFromLoad(AdvancedDynoData data, CarPreset carPreset)
        {
            // Method 3: Load-based calculation (universal fallback)
            // Engine load represents how hard the engine is working

            // Base power from load and RPM
            double baseHP = data.Load * data.Rpm * 0.025; // Empirical scaling factor

            // Displacement scaling
            var displacementFactor = Math.Pow(carPreset.Displacement / 2.0, 0.8);
            baseHP *= displacementFactor;

            // Boost contribution (if available)
            if (data.BoostPressure > 0)
            {
                var boostFactor = 1.0 + (data.BoostPressure * 0.04); // 4% per PSI
                baseHP *= boostFactor;
            }

            return baseHP;
        }

        public double CalculateSimpleAFRCorrection(double afr, bool isForceInduction)
        {
            // Simple AFR correction - just like adjusting for rich/lean conditions
            double optimalAFR = isForceInduction ? 11.8 : 12.8;

            if (afr < 9.0 || afr > 19.0) return 0.95; // Bad data penalty

            double afrDeviation = Math.Abs(afr - optimalAFR);

            if (afrDeviation <= 1.0)
                return 1.0; // Good AFR range
            else if (afrDeviation <= 2.0)
                return 1.0 - (afrDeviation - 1.0) * 0.025; // Gentle penalty
            else
                return 1.0 - 0.05 - (afrDeviation - 2.0) * 0.03; // Steeper penalty
        }

        public double CalculateSimpleAtmosphericCorrection(double intakeTemp)
        {
            if (intakeTemp <= 0) return 1.0;

            // Simple SAE-style temperature correction for air density
            double tempCorrection = Math.Sqrt(537.67 / (intakeTemp + 459.67));

            // Keep it reasonable
            return Math.Max(Math.Min(tempCorrection, 1.05), 0.95);
        }

        public double CalculateSimpleVE(int rpm)
        {
            // Simple volumetric efficiency curve - just engine breathing characteristics
            // No boost corrections - MAF already accounts for actual airflow
            return rpm switch
            {
                < 2500 => 0.90,   // Low RPM
                >= 2500 and < 3200 => 0.90 + (rpm - 2500) * 0.0003,
                >= 3200 and < 3800 => 1.08,  // Building torque
                >= 3800 and < 4200 => 1.12,  // Peak torque zone
                >= 4200 and < 5000 => 1.09,  // Mid-range
                >= 5000 and < 5700 => 1.06,  // Peak HP zone
                >= 5700 and < 6200 => 1.03,  // High RPM
                _ => 0.99    // >= 6200 (Over-rev)
            };
        }

        public double GetCalibrationFactor(string carKey, string calculationMethod)
        {
            // Method-specific calibration factors
            return calculationMethod switch
            {
                "MAF" => GetMAFCalibrationFactor(carKey),
                "MAP" => GetMAPCalibrationFactor(carKey),
                "Load" => GetLoadCalibrationFactor(carKey),
                _ => GetMAFCalibrationFactor(carKey) // Default to MAF calibration
            };
        }

        public double GetMAFCalibrationFactor(string carKey)
        {
            // MAF-based calibration factors (our tested values)
            return carKey switch
            {
                "mazdaspeed3" => 0.895,
                "wrx" => 0.92,
                "sti" => 0.89,
                "evo" => 0.91,
                "gti" => 0.95,
                "focus_st" => 0.93,
                _ => 0.92
            };
        }

        public double GetMAPCalibrationFactor(string carKey)
        {
            // MAP-based calibration factors (tuned to match MAF accuracy)
            // MAP method tends to calculate slightly higher airflow, so we scale down
            return carKey switch
            {
                "mazdaspeed3" => 0.75, // Scaled to match MAF results
                "wrx" => 0.78,         // WRX commonly uses MAP/speed density
                "sti" => 0.76,
                "evo" => 0.77,
                "gti" => 0.80,
                "focus_st" => 0.78,
                _ => 0.78
            };
        }

        public double GetLoadCalibrationFactor(string carKey)
        {
            // Load-based calibration factors (refined)
            return carKey switch
            {
                "mazdaspeed3" => 2.2,  // Adjusted down from 2.8
                "wrx" => 2.4,
                "sti" => 2.3,
                "evo" => 2.35,
                "gti" => 2.5,
                "focus_st" => 2.45,
                _ => 2.4
            };
        }

        public double CalculateTorque(double horsepower, int rpm)
        {
            return rpm > 0 ? (horsepower * 5252) / rpm : 0;
        }

        public List<DynoDataPoint> SmoothDataPreservePeaks(List<DynoDataPoint> data, int smoothingLevel)
        {
            if (smoothingLevel <= 0 || !data.Any()) return data;

            var smoothedData = new List<DynoDataPoint>();

            // First, identify genuine peaks (not just noise spikes)
            var hpPeaks = IdentifyGenuinePeaks(data.Select(d => d.Horsepower).ToList());
            var tqPeaks = IdentifyGenuinePeaks(data.Select(d => d.Torque).ToList());
            var boostPeaks = IdentifyGenuinePeaks(data.Select(d => d.Boost).ToList());

            for (int i = 0; i < data.Count; i++)
            {
                // Check if this point is a genuine peak
                bool isHpPeak = hpPeaks.Contains(i);
                bool isTqPeak = tqPeaks.Contains(i);
                bool isBoostPeak = boostPeaks.Contains(i);

                if (isHpPeak || isTqPeak || isBoostPeak)
                {
                    // PRESERVE PEAKS - don't smooth them!
                    smoothedData.Add(new DynoDataPoint
                    {
                        DynoRunId = data[i].DynoRunId,
                        Rpm = data[i].Rpm,
                        // Keep original peak values
                        Horsepower = isHpPeak ? data[i].Horsepower : SmoothSingleValue(data, i, d => d.Horsepower, smoothingLevel),
                        Torque = isTqPeak ? data[i].Torque : SmoothSingleValue(data, i, d => d.Torque, smoothingLevel),
                        Boost = isBoostPeak ? data[i].Boost : SmoothSingleValue(data, i, d => d.Boost, smoothingLevel),
                        MassAirflow = data[i].MassAirflow,
                        Load = data[i].Load,
                        Timestamp = data[i].Timestamp
                    });
                }
                else
                {
                    // Smooth non-peak areas normally
                    smoothedData.Add(new DynoDataPoint
                    {
                        DynoRunId = data[i].DynoRunId,
                        Rpm = data[i].Rpm,
                        Horsepower = SmoothSingleValue(data, i, d => d.Horsepower, smoothingLevel),
                        Torque = SmoothSingleValue(data, i, d => d.Torque, smoothingLevel),
                        Boost = SmoothSingleValue(data, i, d => d.Boost, smoothingLevel),
                        MassAirflow = data[i].MassAirflow,
                        Load = data[i].Load,
                        Timestamp = data[i].Timestamp
                    });
                }
            }

            return smoothedData;
        }

        public List<int> IdentifyGenuinePeaks(List<double> values)
        {
            var peaks = new List<int>();

            for (int i = 1; i < values.Count - 1; i++)
            {
                // Check if this is a genuine peak (not just noise)
                var current = values[i];
                var left = values[i - 1];
                var right = values[i + 1];

                // Must be higher than both neighbors by meaningful amount
                if (current > left + 2 && current > right + 2)
                {
                    // Additional validation: check wider context
                    var windowStart = Math.Max(0, i - 5);
                    var windowEnd = Math.Min(values.Count - 1, i + 5);
                    var windowMax = values.Skip(windowStart).Take(windowEnd - windowStart + 1).Max();

                    // Only consider it a peak if it's the highest in the local area
                    if (Math.Abs(current - windowMax) < 1.0)
                    {
                        peaks.Add(i);
                    }
                }
            }

            return peaks;
        }

        public double SmoothSingleValue(List<DynoDataPoint> data, int index, Func<DynoDataPoint, double> selector, int smoothingLevel)
        {
            // Reduced window size to prevent over-smoothing
            var windowSize = Math.Min(smoothingLevel / 2, 2); // Much smaller window
            var start = Math.Max(0, index - windowSize);
            var end = Math.Min(data.Count - 1, index + windowSize);

            double totalWeight = 0;
            double weightedValue = 0;

            for (int j = start; j <= end; j++)
            {
                double distance = Math.Abs(index - j);
                double weight = Math.Exp(-distance * distance / (2.0 * smoothingLevel * smoothingLevel));

                weightedValue += selector(data[j]) * weight;
                totalWeight += weight;
            }

            return Math.Round(weightedValue / totalWeight, 1);
        }

        public PeakValues CalculatePeakValues(List<DynoDataPoint> data, int carWeight)
        {
            if (!data.Any()) return new PeakValues();

            // Find peaks with RPM range validation
            var maxHpPoint = data
                .Where(d => d.Rpm >= 4000) // HP peak should be at higher RPM
                .OrderByDescending(d => d.Horsepower)
                .FirstOrDefault() ?? data.OrderByDescending(d => d.Horsepower).First();

            var maxTqPoint = data
                .Where(d => d.Rpm >= 2500 && d.Rpm <= 5500) // Torque peak in reasonable range
                .OrderByDescending(d => d.Torque)
                .FirstOrDefault() ?? data.OrderByDescending(d => d.Torque).First();

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