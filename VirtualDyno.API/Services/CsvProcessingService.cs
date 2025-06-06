using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public class CsvProcessingService : ICsvProcessingService
    {
        private readonly IDynoCalculationService _calculationService;
        private readonly ILogger<CsvProcessingService> _logger;

        // Comprehensive column mapping for different tuning platforms and cars
        private static readonly Dictionary<string, string[]> ColumnMappings = new()
        {
            // Essential parameters (required)
            ["rpm"] = new[] {
                "rpm", "engine speed", "engine rpm", "rpm (rpm)", "engine speed (rpm)"
            },

            ["load"] = new[] {
                "load", "calculated load", "calculated load (load)", "engine load", "load (%)",
                "engine load (%)", "calculated engine load"
            },

            // Primary sensors (at least one required for calculation)
            ["maf"] = new[] {
                "mass airflow", "maf", "mass airflow (g/s)", "maf (g/s)", "mass air flow",
                "airflow", "air flow", "maf sensor"
            },

            ["boost"] = new[] {
                "boost", "boost (psi)", "manifold absolute pressure", "map", "boost pressure",
                "intake manifold pressure", "map (psi)", "map (kpa)", "boost psi"
            },

            // AFR variations by platform
            ["afr"] = new[] {
                "afr", "air fuel", "actual afr", "actual afr (afr)", "afr bank 1", "a/f ratio",
                "lambda", "air fuel ratio", "wideband afr", "wb afr", "air/fuel ratio",
                "fuel ratio", "oxygen sensor", "o2 sensor"
            },

            // Temperature variations (critical for MAP calculations)
            ["intake_temp"] = new[] {
                "intake temp", "iat", "boost air temp", "boost air temp. (f)", "intake air temp",
                "charge air temp", "manifold air temp", "intercooler temp", "post intercooler temp",
                "boost air temp (f)", "intake temp (f)", "iat (f)"
            },

            // Knock detection variations by platform
            ["knock"] = new[] { 
                // Mazda AccessPort
                "knock retard", "knock retard (°)", "knock", "knock timing",
                // Subaru AccessPort
                "feedback knock", "feedback knock (°)", "fine knock learn", "fine knock learn (°)",
                "knock learn", "knock correction", "ignition correction",
                // Honda Hondata
                "knock count", "knock retard cyl", "knock sensor", "knock level",
                // Generic OBD
                "timing advance", "ignition timing retard", "spark retard", "timing retard"
            },

            // Throttle position
            ["throttle"] = new[] {
                "throttle", "throttle position", "accel pedal", "accel. pedal pos.", "tps",
                "accelerator position", "throttle pos", "pedal position", "throttle (%)",
                "throttle position (%)", "accel. pedal pos. (%)"
            },

            // Subaru-specific parameters
            ["dam"] = new[] {
                "dam", "dam (%)", "dynamic advance multiplier", "dam ratio"
            },

            ["af_learn"] = new[] {
                "a/f learning #1", "af learning", "fuel trim", "long term fuel trim",
                "a/f learning #1 (%)", "ltft", "ltft (%)"
            },

            ["af_correction"] = new[] {
                "a/f correction #1", "af correction", "short term fuel trim", "stft",
                "a/f correction #1 (%)", "stft (%)"
            },

            // Honda-specific (MAP in kPa)
            ["map_kpa"] = new[] {
                "map (kpa)", "map kpa", "manifold pressure (kpa)", "intake manifold (kpa)"
            }
        };

        public CsvProcessingService(
            IDynoCalculationService calculationService,
            ILogger<CsvProcessingService> logger)
        {
            _calculationService = calculationService;
            _logger = logger;
        }

        public async Task<DynoRun> ProcessCsvFileAsync(
            Stream csvStream,
            string fileName,
            CarConfiguration carConfig,
            CarPreset carPreset,
            int gear,
            string userId)
        {
            var dataPoints = new List<DynoDataPoint>();

            try
            {
                using var reader = new StreamReader(csvStream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                // Read header to identify columns
                await csv.ReadAsync();
                csv.ReadHeader();
                var headers = csv.HeaderRecord ?? Array.Empty<string>();

                _logger.LogInformation("Processing CSV: {fileName} with {columnCount} columns",
                    fileName, headers.Length);
                _logger.LogInformation("Available columns: {columns}", string.Join(", ", headers));

                // Find required columns using flexible mapping
                var rpmColumn = FindColumn(headers, "rpm");
                var loadColumn = FindColumn(headers, "load");

                // Find primary sensor columns (at least one must be available for calculation)
                var mafColumn = FindColumnOptional(headers, "maf");
                var boostColumn = FindColumnOptional(headers, "boost");
                var mapKpaColumn = FindColumnOptional(headers, "map_kpa");

                // Validate we have enough data for calculation
                bool hasMAF = mafColumn != null;
                bool hasMAP = boostColumn != null || mapKpaColumn != null;

                if (!hasMAF && !hasMAP)
                {
                    throw new InvalidOperationException(
                        "CSV must contain either MAF sensor data (for MAF-based calculation) " +
                        "or Boost/MAP sensor data (for MAP-based calculation). " +
                        $"Available columns: {string.Join(", ", headers)}");
                }

                // Find optional columns with platform-specific variations
                var afrColumn = FindColumnOptional(headers, "afr");
                var intakeTempColumn = FindColumnOptional(headers, "intake_temp");
                var knockColumn = FindColumnOptional(headers, "knock");
                var throttleColumn = FindColumnOptional(headers, "throttle");

                // Subaru-specific optional columns
                var damColumn = FindColumnOptional(headers, "dam");
                var afLearnColumn = FindColumnOptional(headers, "af_learn");
                var afCorrectionColumn = FindColumnOptional(headers, "af_correction");

                // Log column mapping results
                _logger.LogInformation("Column mapping results:");
                _logger.LogInformation("  Required: RPM: {rpm}, Load: {load}", rpmColumn, loadColumn);
                _logger.LogInformation("  Primary sensors: MAF: {maf}, Boost: {boost}, MAP(kPa): {mapKpa}",
                    mafColumn ?? "Not found", boostColumn ?? "Not found", mapKpaColumn ?? "Not found");
                _logger.LogInformation("  Optional: AFR: {afr}, Intake Temp: {temp}, Knock: {knock}",
                    afrColumn ?? "Not found", intakeTempColumn ?? "Not found", knockColumn ?? "Not found");

                // Determine calculation method
                string calculationMethod;
                if (hasMAF)
                {
                    calculationMethod = "MAF-based (most accurate)";
                }
                else if (hasMAP && intakeTempColumn != null)
                {
                    calculationMethod = "MAP-based (speed density)";
                }
                else
                {
                    calculationMethod = "Load-based (fallback)";
                }
                _logger.LogInformation("  Calculation method: {method}", calculationMethod);

                if (damColumn != null || afLearnColumn != null)
                {
                    _logger.LogInformation("  Subaru-specific - DAM: {dam}, AF Learning: {learn}, AF Correction: {corr}",
                        damColumn ?? "Not found", afLearnColumn ?? "Not found", afCorrectionColumn ?? "Not found");
                }

                int rowCount = 0;
                int validRowCount = 0;

                while (await csv.ReadAsync())
                {
                    rowCount++;
                    try
                    {
                        var rpm = csv.GetField<int>(rpmColumn);
                        var load = csv.GetField<double>(loadColumn);

                        // Get sensor data with fallbacks
                        var maf = mafColumn != null ? GetFieldSafe<double>(csv, mafColumn, 0) : 0;
                        var boost = boostColumn != null ? GetFieldSafe<double>(csv, boostColumn, 0) : 0;

                        // Handle Honda MAP in kPa (convert to PSI boost)
                        if (mapKpaColumn != null && boost == 0)
                        {
                            var mapKpa = GetFieldSafe<double>(csv, mapKpaColumn, 0);
                            // Convert absolute MAP to boost: Boost = (MAP - Atmospheric) where Atmospheric ≈ 101.3 kPa
                            boost = (mapKpa - 101.3) * 0.145038; // Convert kPa to PSI
                        }

                        // Filter for valid dyno data - adjust criteria based on available sensors
                        bool isValidData = rpm > 2000 && load > 0.15;

                        if (hasMAF)
                        {
                            isValidData = isValidData && maf > 5; // MAF threshold when available
                        }
                        else if (hasMAP)
                        {
                            isValidData = isValidData && boost > -10; // Boost threshold when no MAF
                        }

                        if (isValidData)
                        {
                            // Create advanced dyno data object with platform-specific handling
                            var advancedData = new AdvancedDynoData
                            {
                                Rpm = rpm,
                                MassAirflow = maf, // May be 0 if no MAF sensor
                                Load = load,
                                BoostPressure = boost, // May be calculated from MAP
                                IsForceInduction = IsForceInduction(carPreset.Key),

                                // Standard optional fields
                                AFR = afrColumn != null ? GetFieldSafe<double>(csv, afrColumn, 0) : 0,
                                IntakeAirTemp = intakeTempColumn != null ? GetFieldSafe<double>(csv, intakeTempColumn, 0) : 0,
                                ThrottlePosition = throttleColumn != null ? GetFieldSafe<double>(csv, throttleColumn, 0) : 0,

                                // Platform-specific knock handling
                                KnockRetard = CalculateKnockRetard(csv, knockColumn, damColumn, afLearnColumn, afCorrectionColumn, carPreset.Key)
                            };

                            var hp = _calculationService.CalculateAdvancedHorsepower(advancedData, gear, carPreset);
                            var torque = _calculationService.CalculateTorque(hp, rpm);

                            dataPoints.Add(new DynoDataPoint
                            {
                                Rpm = rpm,
                                Horsepower = Math.Round(hp, 1),
                                Torque = Math.Round(torque, 1),
                                Boost = Math.Round(boost, 1),
                                MassAirflow = maf,
                                Load = load,
                                Timestamp = DateTime.UtcNow
                            });

                            validRowCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error processing row {row}: {error}", rowCount, ex.Message);
                    }
                }

                _logger.LogInformation("Processed {total} rows, {valid} valid data points using {method}",
                    rowCount, validRowCount, calculationMethod);

                if (!dataPoints.Any())
                {
                    throw new InvalidOperationException(
                        "No valid dyno data found in the CSV file. " +
                        "Ensure the file contains RPM and Load data with either MAF or Boost/MAP sensors. " +
                        "For valid dyno data: RPM > 2000, Load > 15%, and appropriate sensor thresholds.");
                }

                // Set default smoothing to level 1 (light smoothing for best accuracy/smoothness balance)
                // Level 0 = raw data (noisy), Level 1 = light smoothing (recommended default)
                const int defaultSmoothingLevel = 1;

                _logger.LogInformation("Applying default smoothing level {level} to {points} data points",
                    defaultSmoothingLevel, dataPoints.Count);

                // Group by RPM first to average duplicates (this always happens)
                var groupedData = dataPoints
                    .GroupBy(d => d.Rpm)
                    .Select(g => new DynoDataPoint
                    {
                        Rpm = g.Key,
                        Horsepower = Math.Round(g.Average(d => d.Horsepower), 1),
                        Torque = Math.Round(g.Average(d => d.Torque), 1),
                        Boost = Math.Round(g.Average(d => d.Boost), 1),
                        MassAirflow = g.Average(d => d.MassAirflow),
                        Load = g.Average(d => d.Load),
                        Timestamp = g.First().Timestamp
                    })
                    .OrderBy(d => d.Rpm)
                    .ToList();

                // Apply smoothing based on level (0 = none, 1+ = progressive smoothing)
                List<DynoDataPoint> finalData;
                if (defaultSmoothingLevel == 0)
                {
                    finalData = groupedData; // Raw data (no additional smoothing)
                    _logger.LogInformation("No smoothing applied (level 0 - raw data)");
                }
                else
                {
                    var beforeSmoothing = groupedData.ToList(); // Keep original for logging
                    finalData = _calculationService.SmoothDataPreservePeaks(groupedData, defaultSmoothingLevel);

                    // Log smoothing effects
                    LogSmoothingEffects(beforeSmoothing, finalData, defaultSmoothingLevel);
                }

                var dynoRun = new DynoRun
                {
                    UserId = userId,
                    FileName = fileName,
                    DataPoints = finalData,
                    Car = carConfig,
                    Peaks = _calculationService.CalculatePeakValues(finalData, carConfig.Weight),
                    GearUsed = gear,
                    SmoothingLevel = defaultSmoothingLevel, // Default to level 1 (user can change later)
                    CalculationMethod = calculationMethod, // Store which method was used
                    CreatedAt = DateTime.UtcNow
                };

                _logger.LogInformation("Successfully created dyno run with {points} data points. " +
                    "Peak HP: {hp} @ {hpRpm} RPM, Peak Torque: {tq} @ {tqRpm} RPM",
                    groupedData.Count,
                    dynoRun.Peaks?.MaxHorsepower ?? 0,
                    dynoRun.Peaks?.MaxHorsepowerRpm ?? 0,
                    dynoRun.Peaks?.MaxTorque ?? 0,
                    dynoRun.Peaks?.MaxTorqueRpm ?? 0);

                return dynoRun;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file: {fileName}", fileName);
                throw new InvalidOperationException($"Error processing CSV file: {ex.Message}", ex);
            }
        }

        private string FindColumn(string[] headers, string mappingKey)
        {
            if (!ColumnMappings.TryGetValue(mappingKey, out var searchTerms))
                throw new ArgumentException($"Unknown mapping key: {mappingKey}");

            foreach (var term in searchTerms)
            {
                var column = headers.FirstOrDefault(h =>
                    h.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (column != null) return column;
            }

            var availableColumns = string.Join(", ", headers);
            throw new ArgumentException(
                $"Could not find required column for '{mappingKey}'. " +
                $"Searched for: {string.Join(", ", searchTerms)}. " +
                $"Available columns: {availableColumns}");
        }

        private string? FindColumnOptional(string[] headers, string mappingKey)
        {
            if (!ColumnMappings.TryGetValue(mappingKey, out var searchTerms))
                return null;

            foreach (var term in searchTerms)
            {
                var column = headers.FirstOrDefault(h =>
                    h.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (column != null) return column;
            }
            return null;
        }

        private double CalculateKnockRetard(CsvReader csv, string? knockColumn, string? damColumn,
            string? afLearnColumn, string? afCorrectionColumn, string carKey)
        {
            // Platform-specific knock calculation
            if (carKey.Contains("wrx") || carKey.Contains("sti"))
            {
                // Subaru-specific knock handling
                double knockValue = 0;

                // Primary knock indicators
                if (knockColumn != null)
                {
                    knockValue = GetFieldSafe<double>(csv, knockColumn, 0);
                }

                // DAM (Dynamic Advance Multiplier) - values below 1.0 indicate knock learning
                if (damColumn != null)
                {
                    var dam = GetFieldSafe<double>(csv, damColumn, 1.0);
                    if (dam < 1.0)
                    {
                        // Convert DAM reduction to equivalent timing retard
                        knockValue += (1.0 - dam) * 10; // Rough conversion: 10% DAM loss = 1° timing retard
                    }
                }

                // Fuel trim corrections can indicate knock-related enrichment
                if (afLearnColumn != null)
                {
                    var afLearn = GetFieldSafe<double>(csv, afLearnColumn, 0);
                    if (Math.Abs(afLearn) > 5) // Significant fuel trim
                    {
                        knockValue += Math.Abs(afLearn) * 0.1; // Small contribution to knock penalty
                    }
                }

                // Short term fuel corrections
                if (afCorrectionColumn != null)
                {
                    var afCorrection = GetFieldSafe<double>(csv, afCorrectionColumn, 0);
                    if (Math.Abs(afCorrection) > 10) // Large short-term correction
                    {
                        knockValue += Math.Abs(afCorrection) * 0.05; // Smaller contribution
                    }
                }

                return knockValue;
            }
            else
            {
                // Standard knock retard for other platforms (Mazda, Honda, etc.)
                return knockColumn != null ? GetFieldSafe<double>(csv, knockColumn, 0) : 0;
            }
        }

        private T GetFieldSafe<T>(CsvReader csv, string columnName, T defaultValue)
        {
            try
            {
                var value = csv.GetField<T>(columnName);
                return value ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private void LogSmoothingEffects(List<DynoDataPoint> beforeSmoothing, List<DynoDataPoint> afterSmoothing, int smoothingLevel)
        {
            // Find peak values before and after smoothing
            var peakHpBefore = beforeSmoothing.MaxBy(d => d.Horsepower);
            var peakTqBefore = beforeSmoothing.MaxBy(d => d.Torque);
            var peakHpAfter = afterSmoothing.MaxBy(d => d.Horsepower);
            var peakTqAfter = afterSmoothing.MaxBy(d => d.Torque);

            _logger.LogInformation("Smoothing effects (level {level}):", smoothingLevel);
            _logger.LogInformation("  Peak HP: {beforeHp} → {afterHp} HP ({changeHp:+0.0;-0.0} HP change)",
                peakHpBefore?.Horsepower ?? 0,
                peakHpAfter?.Horsepower ?? 0,
                (peakHpAfter?.Horsepower ?? 0) - (peakHpBefore?.Horsepower ?? 0));

            _logger.LogInformation("  Peak Torque: {beforeTq} → {afterTq} lb-ft ({changeTq:+0.0;-0.0} lb-ft change)",
                peakTqBefore?.Torque ?? 0,
                peakTqAfter?.Torque ?? 0,
                (peakTqAfter?.Torque ?? 0) - (peakTqBefore?.Torque ?? 0));

            // Sample a few points to show smoothing effect
            var sampleRpms = new[] { 3500, 4000, 4500, 5000, 5500 };
            _logger.LogInformation("  Sample points smoothing:");

            foreach (var rpm in sampleRpms)
            {
                var beforePoint = beforeSmoothing.FirstOrDefault(d => Math.Abs(d.Rpm - rpm) <= 100);
                var afterPoint = afterSmoothing.FirstOrDefault(d => Math.Abs(d.Rpm - rpm) <= 100);

                if (beforePoint != null && afterPoint != null)
                {
                    _logger.LogInformation("    {rpm} RPM: {beforeHp:F1} → {afterHp:F1} HP ({changeHp:+0.0;-0.0})",
                        rpm,
                        beforePoint.Horsepower,
                        afterPoint.Horsepower,
                        afterPoint.Horsepower - beforePoint.Horsepower);
                }
            }
        }

        private bool IsForceInduction(string carKey)
        {
            return carKey switch
            {
                "mazdaspeed3" => true,
                "wrx" or "sti" => true,
                "evo" => true,
                "gti" => true,
                "focus_st" => true,
                "civic_si" => true, // Newer turbo models
                _ => false // Default to naturally aspirated
            };
        }
    }
}