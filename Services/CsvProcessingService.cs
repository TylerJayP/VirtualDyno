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

                // Find required columns
                var rpmColumn = FindColumn(headers, "rpm");
                var mafColumn = FindColumn(headers, "mass airflow", "maf");
                var boostColumn = FindColumn(headers, "boost");
                var loadColumn = FindColumn(headers, "load");

                // Find optional advanced columns
                var afrColumn = FindColumnOptional(headers, "afr", "air fuel", "actual afr");
                var intakeTempColumn = FindColumnOptional(headers, "intake temp", "iat", "boost air temp");
                var knockColumn = FindColumnOptional(headers, "knock", "knock retard");
                var throttleColumn = FindColumnOptional(headers, "throttle", "accel pedal");

                _logger.LogInformation("Found columns - RPM: {rpm}, MAF: {maf}, Boost: {boost}, Load: {load}",
                    rpmColumn, mafColumn, boostColumn, loadColumn);

                _logger.LogInformation("Advanced columns - AFR: {afr}, Intake Temp: {temp}, Knock: {knock}",
                    afrColumn ?? "Not found", intakeTempColumn ?? "Not found", knockColumn ?? "Not found");

                int rowCount = 0;
                int validRowCount = 0;

                while (await csv.ReadAsync())
                {
                    rowCount++;
                    try
                    {
                        var rpm = csv.GetField<int>(rpmColumn);
                        var maf = csv.GetField<double>(mafColumn);
                        var boost = csv.GetField<double>(boostColumn);
                        var load = csv.GetField<double>(loadColumn);

                        // Filter for valid dyno data
                        if (rpm > 2000 && maf > 5 && load > 0.15)
                        {
                            // Create advanced dyno data object
                            var advancedData = new AdvancedDynoData
                            {
                                Rpm = rpm,
                                MassAirflow = maf,
                                Load = load,
                                BoostPressure = boost,
                                IsForceInduction = IsForceInduction(carPreset.Key),

                                // Optional fields - use 0 if not available
                                AFR = afrColumn != null ? GetFieldSafe<double>(csv, afrColumn, 0) : 0,
                                IntakeAirTemp = intakeTempColumn != null ? GetFieldSafe<double>(csv, intakeTempColumn, 0) : 0,
                                KnockRetard = knockColumn != null ? GetFieldSafe<double>(csv, knockColumn, 0) : 0,
                                ThrottlePosition = throttleColumn != null ? GetFieldSafe<double>(csv, throttleColumn, 0) : 0
                            };

                            // Use the new advanced calculation method
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

                _logger.LogInformation("Processed {total} rows, {valid} valid data points",
                    rowCount, validRowCount);

                if (!dataPoints.Any())
                {
                    throw new InvalidOperationException("No valid dyno data found in the CSV file");
                }

                // Group by RPM and average duplicates
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

                var dynoRun = new DynoRun
                {
                    UserId = userId,
                    FileName = fileName,
                    DataPoints = groupedData,
                    Car = carConfig,
                    Peaks = _calculationService.CalculatePeakValues(groupedData, carConfig.Weight),
                    GearUsed = gear,
                    CreatedAt = DateTime.UtcNow
                };

                return dynoRun;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file: {fileName}", fileName);
                throw new InvalidOperationException($"Error processing CSV file: {ex.Message}", ex);
            }
        }

        private string FindColumn(string[] headers, params string[] searchTerms)
        {
            foreach (var term in searchTerms)
            {
                var column = headers.FirstOrDefault(h =>
                    h.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (column != null) return column;
            }

            var availableColumns = string.Join(", ", headers);
            throw new ArgumentException(
                $"Could not find column matching: {string.Join(", ", searchTerms)}. " +
                $"Available columns: {availableColumns}");
        }

        private string? FindColumnOptional(string[] headers, params string[] searchTerms)
        {
            foreach (var term in searchTerms)
            {
                var column = headers.FirstOrDefault(h =>
                    h.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (column != null) return column;
            }
            return null;
        }

        private T GetFieldSafe<T>(CsvReader csv, string columnName, T defaultValue)
        {
            try
            {
                return csv.GetField<T>(columnName);
            }
            catch
            {
                return defaultValue;
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
                _ => false
            };
        }
    }
}