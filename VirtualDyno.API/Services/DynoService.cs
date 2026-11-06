using System.Text.Json;
using System.Text.Json.Serialization;
using VirtualDyno.API.Contracts;
using VirtualDyno.API.Data;
using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Logs;

namespace VirtualDyno.API.Services;

/// <summary>Application layer between the controllers and the engine / storage.</summary>
public sealed class DynoService(IRunRepository runs, ILogger<DynoService> logger)
{
    public const long MaxLogBytes = 20 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".csv", ".txt", ".log"];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public DynoResult Analyze(string logText, DynoSettings settings) =>
        DynoAnalyzer.Analyze(LogParser.Parse(logText), settings);

    public async Task<RunDetailDto> SaveAsync(string fileName, string logText, DynoSettings settings, string? name, string? notes,
        CancellationToken ct = default)
    {
        var result = Analyze(logText, settings);   // a log that can't be analyzed is never saved

        var entity = new DynoRunEntity
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(fileName) : name.Trim(),
            FileName = fileName,
            Notes = notes,
            LogText = logText,
            SettingsJson = JsonSerializer.Serialize(settings, Json),
        };
        ApplySummary(entity, result, settings);

        var saved = await runs.AddAsync(entity, ct);
        logger.LogInformation("Saved run {Id} ({Name}): {Whp} whp / {Wtq} wtq", saved.Id, saved.Name, saved.PeakWhp, saved.PeakWtq);
        return new RunDetailDto(RunSummaryDto.From(saved), settings, result);
    }

    public async Task<IReadOnlyList<RunSummaryDto>> ListAsync(CancellationToken ct = default) =>
        (await runs.ListAsync(ct)).Select(RunSummaryDto.From).ToList();

    /// <summary>
    /// Recompute a saved run. Correction and smoothing can be changed for viewing without changing
    /// what's saved, so overlays can be compared on the same basis.
    /// </summary>
    public async Task<RunDetailDto?> GetAsync(int id, CorrectionMode? correction = null, int? smoothingBias = null,
        CancellationToken ct = default)
    {
        var entity = await runs.GetAsync(id, ct);
        if (entity is null) return null;

        var settings = ReadSettings(entity.SettingsJson);
        var view = settings with
        {
            Correction = correction ?? settings.Correction,
            SmoothingBias = smoothingBias ?? settings.SmoothingBias,
        };
        return new RunDetailDto(RunSummaryDto.From(entity), settings, Analyze(entity.LogText, view));
    }

    public async Task<RunDetailDto?> UpdateAsync(int id, UpdateRunRequest request, CancellationToken ct = default)
    {
        var entity = await runs.GetAsync(id, ct);
        if (entity is null) return null;

        var settings = request.Settings ?? ReadSettings(entity.SettingsJson);
        var result = Analyze(entity.LogText, settings);

        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (request.Notes is not null) entity.Notes = request.Notes;
        entity.SettingsJson = JsonSerializer.Serialize(settings, Json);
        entity.UpdatedAtUtc = DateTime.UtcNow;
        ApplySummary(entity, result, settings);

        if (!await runs.UpdateAsync(entity, ct)) return null;
        return new RunDetailDto(RunSummaryDto.From(entity), settings, result);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => runs.DeleteAsync(id, ct);

    public async Task<(string FileName, string Text)?> GetLogAsync(int id, CancellationToken ct = default)
    {
        var entity = await runs.GetAsync(id, ct);
        return entity is null ? null : (entity.FileName, entity.LogText);
    }

    // ---- input helpers ----------------------------------------------------------------------

    public static async Task<(string FileName, string Text)> ReadUploadAsync(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) throw new InvalidUploadException("Choose a datalog file to upload.");
        if (file.Length > MaxLogBytes) throw new InvalidUploadException($"Logs can be up to {MaxLogBytes / 1024 / 1024} MB.");

        string name = Path.GetFileName(file.FileName);
        if (!AllowedExtensions.Contains(Path.GetExtension(name).ToLowerInvariant()))
            throw new InvalidUploadException("Upload the log as a .csv file.");

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return (name, LogParser.Decode(buffer.ToArray()));
    }

    public static DynoSettings ReadSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new DynoSettings();
        try
        {
            return JsonSerializer.Deserialize<DynoSettings>(json, Json) ?? new DynoSettings();
        }
        catch (JsonException ex)
        {
            throw new InvalidUploadException($"Settings aren't valid: {ex.Message}");
        }
    }

    private static void ApplySummary(DynoRunEntity entity, DynoResult result, DynoSettings settings)
    {
        entity.VehicleKey = result.Vehicle.Key;
        entity.VehicleName = result.Vehicle.Name;
        entity.Gear = result.Gearing.DetectedGear;
        entity.WeightLb = result.Vehicle.WeightLb;
        entity.Correction = settings.Correction.ToString();
        entity.PeakWhp = result.Peaks.Whp;
        entity.PeakWhpRpm = result.Peaks.WhpRpm;
        entity.PeakWtq = result.Peaks.Wtq;
        entity.PeakWtqRpm = result.Peaks.WtqRpm;
        entity.PeakBoostPsi = result.Peaks.BoostPsi;
    }
}

/// <summary>The request itself is wrong (missing file, wrong type, bad settings). Becomes a 400.</summary>
public sealed class InvalidUploadException(string message) : Exception(message);
