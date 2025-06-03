using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualDyno.API.Data;
using VirtualDyno.API.Models;
using VirtualDyno.API.Models.DTOs;
using VirtualDyno.API.Services;

namespace VirtualDyno.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DynoController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICsvProcessingService _csvService;
        private readonly IDynoCalculationService _calculationService;
        private readonly ILogger<DynoController> _logger;

        public DynoController(
            ApplicationDbContext context,
            ICsvProcessingService csvService,
            IDynoCalculationService calculationService,
            ILogger<DynoController> logger)
        {
            _context = context;
            _csvService = csvService;
            _calculationService = calculationService;
            _logger = logger;
        }

        /// <summary>
        /// Upload and process a dyno CSV file
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult<DynoRunResponseDto>> UploadFile([FromForm] DynoRunCreateDto request)
        {
            try
            {
                // Validate file
                if (request.File == null || request.File.Length == 0)
                    return BadRequest("No file uploaded");

                if (!request.File.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    return BadRequest("Only CSV files are supported");

                if (request.File.Length > 10 * 1024 * 1024) // 10MB limit
                    return BadRequest("File size cannot exceed 10MB");

                // Get car preset
                var carPreset = await _context.CarPresets
                    .FirstOrDefaultAsync(c => c.Key == request.CarPresetKey);

                if (carPreset == null)
                    return BadRequest($"Car preset '{request.CarPresetKey}' not found");

                // Create car configuration
                var carConfig = new CarConfiguration
                {
                    Name = carPreset.Name,
                    Weight = request.Weight > 0 ? request.Weight : carPreset.Weight,
                    Displacement = carPreset.Displacement,
                    DriveType = carPreset.DriveType,
                    PresetKey = request.CarPresetKey
                };

                // For demo purposes, we'll use a default user ID
                // In a real app, this would come from authentication
                var userId = "demo-user";

                // Process the CSV file
                using var stream = request.File.OpenReadStream();
                var dynoRun = await _csvService.ProcessCsvFileAsync(
                    stream,
                    request.File.FileName,
                    carConfig,
                    carPreset,
                    request.Gear,
                    userId);

                dynoRun.Notes = request.Notes;
                dynoRun.IsPublic = request.IsPublic;

                // Save to database
                _context.DynoRuns.Add(dynoRun);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Successfully processed dyno run {id} for file {fileName}",
                    dynoRun.Id, request.File.FileName);

                // Return response DTO
                var response = new DynoRunResponseDto
                {
                    Id = dynoRun.Id,
                    FileName = dynoRun.FileName,
                    CreatedAt = dynoRun.CreatedAt,
                    Car = new CarConfigurationDto
                    {
                        Name = dynoRun.Car.Name,
                        Weight = dynoRun.Car.Weight,
                        Displacement = dynoRun.Car.Displacement,
                        DriveType = dynoRun.Car.DriveType
                    },
                    GearUsed = dynoRun.GearUsed,
                    Peaks = dynoRun.Peaks != null ? new PeakValuesDto
                    {
                        MaxHorsepower = dynoRun.Peaks.MaxHorsepower,
                        MaxHorsepowerRpm = dynoRun.Peaks.MaxHorsepowerRpm,
                        MaxTorque = dynoRun.Peaks.MaxTorque,
                        MaxTorqueRpm = dynoRun.Peaks.MaxTorqueRpm,
                        MaxBoost = dynoRun.Peaks.MaxBoost,
                        PowerToWeightRatio = dynoRun.Peaks.PowerToWeightRatio
                    } : null,
                    Notes = dynoRun.Notes,
                    IsPublic = dynoRun.IsPublic,
                    DataPointCount = dynoRun.DataPoints.Count
                };

                return CreatedAtAction(nameof(GetRun), new { id = dynoRun.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing uploaded file: {fileName}", request.File?.FileName);
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all dyno runs for the current user
        /// </summary>
        [HttpGet("runs")]
        public async Task<ActionResult<List<DynoRunResponseDto>>> GetUserRuns()
        {
            try
            {
                var userId = "demo-user"; // In real app, get from authentication

                var runs = await _context.DynoRuns
                    .Where(r => r.UserId == userId)
                    .Include(r => r.Peaks)
                    .Include(r => r.Car)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new DynoRunResponseDto
                    {
                        Id = r.Id,
                        FileName = r.FileName,
                        CreatedAt = r.CreatedAt,
                        Car = new CarConfigurationDto
                        {
                            Name = r.Car.Name,
                            Weight = r.Car.Weight,
                            Displacement = r.Car.Displacement,
                            DriveType = r.Car.DriveType
                        },
                        GearUsed = r.GearUsed,
                        Peaks = r.Peaks != null ? new PeakValuesDto
                        {
                            MaxHorsepower = r.Peaks.MaxHorsepower,
                            MaxHorsepowerRpm = r.Peaks.MaxHorsepowerRpm,
                            MaxTorque = r.Peaks.MaxTorque,
                            MaxTorqueRpm = r.Peaks.MaxTorqueRpm,
                            MaxBoost = r.Peaks.MaxBoost,
                            PowerToWeightRatio = r.Peaks.PowerToWeightRatio
                        } : null,
                        Notes = r.Notes,
                        IsPublic = r.IsPublic,
                        DataPointCount = r.DataPoints.Count
                    })
                    .ToListAsync();

                return Ok(runs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user runs");
                return StatusCode(500, "Error retrieving dyno runs");
            }
        }

        /// <summary>
        /// Get a specific dyno run with all data points
        /// </summary>
        [HttpGet("runs/{id}")]
        public async Task<ActionResult<DynoRunDetailDto>> GetRun(int id)
        {
            try
            {
                var userId = "demo-user"; // In real app, get from authentication

                var run = await _context.DynoRuns
                    .Where(r => r.Id == id && r.UserId == userId)
                    .Include(r => r.DataPoints.OrderBy(dp => dp.Rpm))
                    .Include(r => r.Peaks)
                    .Include(r => r.Car)
                    .FirstOrDefaultAsync();

                if (run == null)
                    return NotFound($"Dyno run {id} not found");

                var response = new DynoRunDetailDto
                {
                    Id = run.Id,
                    FileName = run.FileName,
                    CreatedAt = run.CreatedAt,
                    Car = new CarConfigurationDto
                    {
                        Name = run.Car.Name,
                        Weight = run.Car.Weight,
                        Displacement = run.Car.Displacement,
                        DriveType = run.Car.DriveType
                    },
                    GearUsed = run.GearUsed,
                    Peaks = run.Peaks != null ? new PeakValuesDto
                    {
                        MaxHorsepower = run.Peaks.MaxHorsepower,
                        MaxHorsepowerRpm = run.Peaks.MaxHorsepowerRpm,
                        MaxTorque = run.Peaks.MaxTorque,
                        MaxTorqueRpm = run.Peaks.MaxTorqueRpm,
                        MaxBoost = run.Peaks.MaxBoost,
                        PowerToWeightRatio = run.Peaks.PowerToWeightRatio
                    } : null,
                    Notes = run.Notes,
                    IsPublic = run.IsPublic,
                    DataPointCount = run.DataPoints.Count,
                    DataPoints = run.DataPoints.Select(dp => new DynoDataPointDto
                    {
                        Rpm = dp.Rpm,
                        Horsepower = dp.Horsepower,
                        Torque = dp.Torque,
                        Boost = dp.Boost
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dyno run {id}", id);
                return StatusCode(500, "Error retrieving dyno run");
            }
        }

        /// <summary>
        /// Get smoothed data for a dyno run
        /// </summary>
        [HttpGet("runs/{id}/smooth/{level}")]
        public async Task<ActionResult<List<DynoDataPointDto>>> GetSmoothedData(int id, int level)
        {
            try
            {
                if (level < 1 || level > 5)
                    return BadRequest("Smoothing level must be between 1 and 5");

                var userId = "demo-user";

                var dataPoints = await _context.DynoDataPoints
                    .Where(dp => dp.DynoRunId == id && dp.DynoRun.UserId == userId)
                    .OrderBy(dp => dp.Rpm)
                    .ToListAsync();

                if (!dataPoints.Any())
                    return NotFound($"No data points found for dyno run {id}");

                var smoothedData = _calculationService.SmoothData(dataPoints, level);

                var response = smoothedData.Select(dp => new DynoDataPointDto
                {
                    Rpm = dp.Rpm,
                    Horsepower = dp.Horsepower,
                    Torque = dp.Torque,
                    Boost = dp.Boost
                }).ToList();

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving smoothed data for run {id}", id);
                return StatusCode(500, "Error retrieving smoothed data");
            }
        }

        /// <summary>
        /// Delete a dyno run
        /// </summary>
        [HttpDelete("runs/{id}")]
        public async Task<IActionResult> DeleteRun(int id)
        {
            try
            {
                var userId = "demo-user";

                var run = await _context.DynoRuns
                    .Where(r => r.Id == id && r.UserId == userId)
                    .FirstOrDefaultAsync();

                if (run == null)
                    return NotFound($"Dyno run {id} not found");

                _context.DynoRuns.Remove(run);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted dyno run {id}", id);

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting dyno run {id}", id);
                return StatusCode(500, "Error deleting dyno run");
            }
        }
    }
}