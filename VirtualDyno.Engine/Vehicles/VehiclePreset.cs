using System.Text.RegularExpressions;

namespace VirtualDyno.Engine.Vehicles;

public enum Drivetrain { Fwd, Rwd, Awd }

/// <summary>Factory specs for one car generation. Only weight and drag area affect the power number;
/// gearing and tire size are used to name the gear and to handle logs without vehicle speed.</summary>
public sealed record VehiclePreset
{
    public required string Key { get; init; }
    public required string Make { get; init; }
    public required string Model { get; init; }
    public required int YearFrom { get; init; }
    public required int YearTo { get; init; }

    /// <summary>Manufacturer curb weight (no driver), manual transmission where offered.</summary>
    public required int CurbWeightLb { get; init; }
    public required Drivetrain Drive { get; init; }
    public bool Turbo { get; init; } = true;

    public required double DragCoefficient { get; init; }
    /// <summary>Frontal area. Where the maker doesn't publish it, estimated as 0.84 × width × height.</summary>
    public required double FrontalAreaM2 { get; init; }

    /// <summary>Forward gear ratios, 1st first.</summary>
    public double[] GearRatios { get; init; } = [];
    /// <summary>One final drive for all gears, or one per gear for dual-final-drive gearboxes.</summary>
    public double[] FinalDrives { get; init; } = [];
    public string? StockTire { get; init; }

    /// <summary>Words that must all appear in an AccessPort log's vehicle description.</summary>
    public string[] MatchTokens { get; init; } = [];
    /// <summary>Words that rule this preset out (e.g. "STI" for a WRX).</summary>
    public string[] ExcludeTokens { get; init; } = [];

    /// <summary>Where the numbers came from, and which ones are estimates.</summary>
    public string? SpecNotes { get; init; }

    public string Name => $"{YearFrom}–{YearTo} {Make} {Model}";
    public double DragAreaM2 => DragCoefficient * FrontalAreaM2;
    public bool HasGearing => GearRatios.Length > 0 && FinalDrives.Length > 0 && StockTire is not null;

    public double? OverallRatio(int gear)
    {
        if (gear < 1 || gear > GearRatios.Length || FinalDrives.Length == 0) return null;
        double fd = FinalDrives.Length == 1 ? FinalDrives[0] : FinalDrives[Math.Min(gear, FinalDrives.Length) - 1];
        return GearRatios[gear - 1] * fd;
    }
}

public static partial class TireSize
{
    /// <summary>Overall diameter in inches for sizes like "225/40R18", "P215/45ZR17", "245/35-19".</summary>
    public static double? DiameterInches(string? size)
    {
        if (string.IsNullOrWhiteSpace(size)) return null;
        var m = Pattern().Match(size.ToUpperInvariant());
        if (!m.Success) return null;
        double width = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        double aspect = double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
        double rim = double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        return rim + 2 * width * aspect / 100.0 / 25.4;
    }

    /// <summary>
    /// A loaded, rolling tire covers less ground per turn than its unloaded size suggests: the contact
    /// patch flattens it. 0.97 of the free circumference is the usual effective rolling circumference.
    /// </summary>
    public const double RollingCircumferenceFactor = 0.97;

    /// <summary>Engine RPM per mph for a given overall ratio and tire.</summary>
    public static double RpmPerMph(double overallRatio, double tireDiameterIn) =>
        overallRatio * Units.InchesPerMile / (Math.PI * tireDiameterIn * RollingCircumferenceFactor) / 60.0;

    /// <summary>Overall ratio (gear × final drive) implied by a measured RPM per mph.</summary>
    public static double OverallRatio(double rpmPerMph, double tireDiameterIn) =>
        rpmPerMph * 60.0 * Math.PI * tireDiameterIn * RollingCircumferenceFactor / Units.InchesPerMile;

    [GeneratedRegex(@"(\d{3})\s*/\s*(\d{2})\s*[A-Z]*\s*-?\s*R?\s*(\d{2})")]
    private static partial Regex Pattern();
}
