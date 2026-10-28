using System.Text.RegularExpressions;

namespace VirtualDyno.Engine.Vehicles;

/// <summary>Built-in vehicle presets plus lookups: by key, by AccessPort log header, and gear detection.</summary>
public static partial class VehicleCatalog
{
    public static IReadOnlyList<VehiclePreset> All => CatalogData.Presets;

    public static VehiclePreset? Find(string? key) =>
        key is null ? null : All.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Match an AccessPort vehicle description such as "USDM 2011-2013 MAZDASPEED3 (Fed.)" to a preset:
    /// every match token must appear, no exclude token may appear, and the model years must overlap.
    /// </summary>
    public static VehiclePreset? MatchLogVehicle(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        string text = " " + Words().Replace(description.ToUpperInvariant(), " ") + " ";
        var years = Years().Matches(description).Select(m => int.Parse(m.Value)).ToList();
        int? from = years.Count > 0 ? years.Min() : null;
        int? to = years.Count > 0 ? years.Max() : null;

        VehiclePreset? best = null;
        int bestScore = int.MinValue;
        foreach (var p in All)
        {
            if (p.MatchTokens.Length == 0) continue;
            if (!p.MatchTokens.All(t => text.Contains(" " + t.ToUpperInvariant() + " "))) continue;
            if (p.ExcludeTokens.Any(t => text.Contains(" " + t.ToUpperInvariant() + " "))) continue;

            int overlap = 0;
            if (from is not null && to is not null)
            {
                overlap = Math.Min(to.Value, p.YearTo) - Math.Max(from.Value, p.YearFrom);
                if (overlap < 0) continue;
            }
            int score = p.MatchTokens.Length * 100 + overlap;
            if (score > bestScore)
            {
                best = p;
                bestScore = score;
            }
        }
        return best;
    }

    /// <summary>
    /// Name the gear from the measured RPM-per-mph. Returns null when nothing is within 5%
    /// (non-stock tires, a different gearbox, or a car without gearing data).
    /// </summary>
    public static (int Gear, double OverallRatio)? DetectGear(VehiclePreset preset, double rpmPerMph, string? tireOverride = null)
    {
        double? diameter = TireSize.DiameterInches(tireOverride ?? preset.StockTire);
        if (diameter is null || preset.GearRatios.Length == 0 || preset.FinalDrives.Length == 0) return null;

        (int Gear, double Ratio, double Error)? best = null;
        for (int g = 1; g <= preset.GearRatios.Length; g++)
        {
            double overall = preset.OverallRatio(g)!.Value;
            double expected = TireSize.RpmPerMph(overall, diameter.Value);
            double error = Math.Abs(rpmPerMph / expected - 1);
            if (best is null || error < best.Value.Error) best = (g, overall, error);
        }
        return best is { Error: < 0.05 } b ? (b.Gear, TireSize.OverallRatio(rpmPerMph, diameter.Value)) : null;
    }

    [GeneratedRegex(@"[^A-Z0-9]+")]
    private static partial Regex Words();

    [GeneratedRegex(@"(19|20)\d{2}")]
    private static partial Regex Years();
}
