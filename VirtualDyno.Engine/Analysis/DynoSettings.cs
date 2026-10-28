namespace VirtualDyno.Engine.Analysis;

/// <summary>
/// Everything the user can tell the dyno. Anything left null is resolved from the log, the vehicle
/// catalog, or a documented default, and the result reports where each value came from.
/// </summary>
public sealed record DynoSettings
{
    /// <summary>Catalog key. Null = identify the car from the log header.</summary>
    public string? VehicleKey { get; init; }

    /// <summary>Total moving weight: car + driver + passengers + fuel. The single most important input.</summary>
    public double? WeightLb { get; init; }

    /// <summary>Drag coefficient × frontal area (m²). Null = from the vehicle catalog.</summary>
    public double? DragAreaM2 { get; init; }

    /// <summary>Tire rolling resistance coefficient. 0.010–0.013 is typical for performance tires.</summary>
    public double RollingResistance { get; init; } = 0.012;

    /// <summary>
    /// Effective-mass factor for the four wheels and tires spinning up with the car.
    /// 1.03 ≈ 18" wheels on a compact car. Engine and gearbox inertia are deliberately not included:
    /// a chassis dyno doesn't count them either, which is why lower gears read lower.
    /// </summary>
    public double RotationalMassFactor { get; init; } = 1.03;

    /// <summary>Road grade in percent (+ = uphill). Average runs in both directions to cancel it.</summary>
    public double RoadGradePct { get; init; }

    /// <summary>Elevation where the log was recorded. Used for barometric pressure if the log has none.</summary>
    public double? AltitudeFt { get; init; }

    /// <summary>Barometric pressure override (inHg, station pressure — not sea-level-adjusted).</summary>
    public double? BaroInHg { get; init; }

    /// <summary>Ambient temperature override. Null = the log's intake air temperature, if present.</summary>
    public double? AmbientTempF { get; init; }

    public double HumidityPct { get; init; } = 30;

    public CorrectionMode Correction { get; init; } = CorrectionMode.Sae;

    /// <summary>0 = automatic (estimated from the log by REML). Each step up doubles the smoothing; each step down halves it.</summary>
    public int SmoothingBias { get; init; }

    /// <summary>Which pull to analyze when the log has several. Null = the widest RPM sweep.</summary>
    public int? PullIndex { get; init; }

    /// <summary>Speedometer error in percent (+ = speedo reads high). Matters for non-stock tire sizes.</summary>
    public double SpeedometerErrorPct { get; init; }

    /// <summary>Only needed when the log has no vehicle speed: the gear the pull was made in.</summary>
    public int? Gear { get; init; }

    /// <summary>Only needed when the log has no vehicle speed and the car isn't in the catalog: gear × final drive.</summary>
    public double? OverallRatio { get; init; }

    /// <summary>Tire size like "225/40R18". Null = the catalog's stock size.</summary>
    public string? TireSize { get; init; }
}
