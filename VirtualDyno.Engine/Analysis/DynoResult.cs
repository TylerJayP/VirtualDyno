using VirtualDyno.Engine.Logs;
using VirtualDyno.Engine.Vehicles;

namespace VirtualDyno.Engine.Analysis;

public enum ValueSource { User, Log, Catalog, Default, Measured }

public enum WarningSeverity { Info, Warning }

public sealed record DynoWarning(string Code, WarningSeverity Severity, string Message);

/// <summary>One point on the dyno sheet. Power and torque are at the wheels, already corrected.</summary>
public sealed record DynoPoint(
    double TimeSec,
    double Rpm,
    double Whp,
    double Wtq,
    double Mph,
    double? BoostPsi,
    double? Afr,
    double? SparkAdvance,
    double? KnockRetard,
    double? IntakeTempF,
    double? ChargeTempF,
    bool Slip);

public sealed record VehicleUsed(
    string? Key,
    string Name,
    bool AutoDetected,
    double WeightLb,
    ValueSource WeightSource,
    double DragAreaM2,
    ValueSource DragAreaSource,
    Drivetrain Drive,
    bool Turbo,
    double RollingResistance,
    double RotationalMassFactor,
    double RoadGradePct);

public sealed record GearingInfo(
    double RpmPerMph,
    ValueSource Source,
    int? DetectedGear,
    double? OverallRatio,
    string? TireSize,
    int SlipPoints,
    double? SlipFromRpm,
    double? SlipToRpm);

public sealed record EnvironmentInfo(
    double BaroInHg,
    ValueSource BaroSource,
    double AmbientTempF,
    ValueSource AmbientSource,
    double HumidityPct,
    double AirDensityKgM3,
    CorrectionMode Correction,
    double CorrectionFactor);

public sealed record PeakValues(
    double Whp,
    double WhpRpm,
    double Wtq,
    double WtqRpm,
    double? BoostPsi,
    double UncorrectedWhp,
    double UncorrectedWtq);

/// <summary>Where the power went at the peak-power point: what the car was fighting.</summary>
public sealed record PowerBreakdown(double AccelerationHp, double AeroHp, double RollingHp, double GradeHp);

/// <summary>
/// Independent fuel-energy estimate from MAF and AFR. Fuel burned per hour divided by power gives
/// brake-specific fuel consumption (BSFC). Real engines live in a narrow BSFC band, so a physics result
/// that implies an impossible BSFC means something else is off: weight, grade, or wheelspin.
/// </summary>
public sealed record MafCrossCheck(
    double MafGs,
    double Afr,
    bool AfrAssumed,
    double FuelLbPerHr,
    double ImpliedBsfc,
    double ExpectedBsfcLow,
    double ExpectedBsfcHigh,
    double EstimatedWhpLow,
    double EstimatedWhpHigh,
    bool Agrees);

public sealed record SmoothingInfo(int Bias, double Lambda, double EffectiveDegreesOfFreedom, double SampleRateHz);

public sealed record LogInfo(string? Device, string? Firmware, string? Vehicle, string? Map, int Rows, IReadOnlyList<ChannelMatch> Channels);

public sealed record DynoResult(
    LogInfo Log,
    VehicleUsed Vehicle,
    IReadOnlyList<Pull> Pulls,
    int SelectedPull,
    GearingInfo Gearing,
    EnvironmentInfo Environment,
    SmoothingInfo Smoothing,
    PeakValues Peaks,
    PowerBreakdown Breakdown,
    MafCrossCheck? CrossCheck,
    IReadOnlyList<DynoPoint> Points,
    IReadOnlyList<DynoWarning> Warnings);

/// <summary>The log can't produce a dyno result (no pull, missing channels...). The message is user-facing.</summary>
public sealed class DynoAnalysisException(string message) : Exception(message);
