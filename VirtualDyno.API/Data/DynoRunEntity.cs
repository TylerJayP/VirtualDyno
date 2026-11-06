namespace VirtualDyno.API.Data;

/// <summary>
/// A saved dyno run. The original log and the settings are stored, not the computed curve, so every
/// run can be recomputed (and benefits from any engine improvement) at any time. The peak columns are
/// a cache for the runs list.
/// </summary>
public class DynoRunEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The datalog exactly as uploaded (decoded to text).</summary>
    public string LogText { get; set; } = string.Empty;

    /// <summary><see cref="VirtualDyno.Engine.Analysis.DynoSettings"/> as JSON.</summary>
    public string SettingsJson { get; set; } = "{}";

    // ---- summary cache (refreshed whenever the run is saved or its settings change) ----
    public string? VehicleKey { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public int? Gear { get; set; }
    public double WeightLb { get; set; }
    public string Correction { get; set; } = string.Empty;
    public double PeakWhp { get; set; }
    public double PeakWhpRpm { get; set; }
    public double PeakWtq { get; set; }
    public double PeakWtqRpm { get; set; }
    public double? PeakBoostPsi { get; set; }
}
