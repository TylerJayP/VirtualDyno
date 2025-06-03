using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VirtualDyno.API.Data;
using VirtualDyno.API.Models.DTOs;

namespace VirtualDyno.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarPresetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CarPresetsController> _logger;

        public CarPresetsController(ApplicationDbContext context, ILogger<CarPresetsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all available car presets
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<List<CarPresetDto>>> GetCarPresets()
        {
            try
            {
                var presets = await _context.CarPresets
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .Select(p => new CarPresetDto
                    {
                        Key = p.Key,
                        Name = p.Name,
                        Weight = p.Weight,
                        Displacement = p.Displacement,
                        DriveType = p.DriveType,
                        GearRatios = new Dictionary<int, double>
                        {
                            { 3, p.GearRatio3rd },
                            { 4, p.GearRatio4th },
                            { 5, p.GearRatio5th }
                        },
                        FinalDrive = p.FinalDrive
                    })
                    .ToListAsync();

                return Ok(presets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving car presets");
                return StatusCode(500, "Error retrieving car presets");
            }
        }

        /// <summary>
        /// Get a specific car preset by key
        /// </summary>
        [HttpGet("{key}")]
        public async Task<ActionResult<CarPresetDto>> GetCarPreset(string key)
        {
            try
            {
                var preset = await _context.CarPresets
                    .Where(p => p.Key == key && p.IsActive)
                    .FirstOrDefaultAsync();

                if (preset == null)
                    return NotFound($"Car preset '{key}' not found");

                var response = new CarPresetDto
                {
                    Key = preset.Key,
                    Name = preset.Name,
                    Weight = preset.Weight,
                    Displacement = preset.Displacement,
                    DriveType = preset.DriveType,
                    GearRatios = new Dictionary<int, double>
                    {
                        { 3, preset.GearRatio3rd },
                        { 4, preset.GearRatio4th },
                        { 5, preset.GearRatio5th }
                    },
                    FinalDrive = preset.FinalDrive
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving car preset {key}", key);
                return StatusCode(500, "Error retrieving car preset");
            }
        }
    }
}