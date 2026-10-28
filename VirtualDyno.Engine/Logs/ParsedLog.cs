namespace VirtualDyno.Engine.Logs;

/// <summary>Signals the engine understands. Every channel is stored in one canonical unit.</summary>
public enum Channel
{
    /// <summary>Seconds.</summary>
    Time,
    /// <summary>Engine RPM.</summary>
    Rpm,
    /// <summary>Vehicle speed, mph.</summary>
    Speed,
    /// <summary>Accelerator pedal, %.</summary>
    Pedal,
    /// <summary>Throttle plate, %.</summary>
    Throttle,
    /// <summary>Boost, psi gauge (relative to atmosphere).</summary>
    Boost,
    /// <summary>Manifold absolute pressure, psia. Converted to boost once barometric pressure is known.</summary>
    ManifoldAbsolute,
    /// <summary>Air/fuel ratio (gasoline scale).</summary>
    Afr,
    /// <summary>Mass airflow, g/s.</summary>
    Maf,
    /// <summary>Pre-turbo / intake air temperature, °F. Close to ambient on a moving car.</summary>
    IntakeTemp,
    /// <summary>Post-intercooler charge temperature, °F.</summary>
    ChargeTemp,
    /// <summary>Knock retard, degrees (positive = timing pulled).</summary>
    KnockRetard,
    /// <summary>Ignition advance, degrees.</summary>
    SparkAdvance,
    /// <summary>Engine load, as logged.</summary>
    Load,
    /// <summary>Barometric pressure, inHg.</summary>
    Baro,
    /// <summary>Gear position, as logged.</summary>
    Gear,
}

/// <summary>Which column fed a channel, and how it was converted.</summary>
public sealed record ChannelMatch(Channel Channel, string Column, string? SourceUnit, string? Conversion);

/// <summary>What the logging device wrote about itself (AccessPort logs carry this in the header).</summary>
public sealed record LogSource(string? Device, string? Firmware, string? Vehicle, string? Map);

/// <summary>A datalog with its columns mapped to known channels in canonical units. Missing cells are NaN.</summary>
public sealed class ParsedLog
{
    public required IReadOnlyList<string> Columns { get; init; }
    public required IReadOnlyDictionary<Channel, double[]> Data { get; init; }
    public required IReadOnlyList<ChannelMatch> Matches { get; init; }
    public required LogSource Source { get; init; }
    public int RowCount { get; init; }

    public bool Has(Channel channel) => Data.ContainsKey(channel);

    public double[] this[Channel channel] => Data[channel];

    public double[]? TryGet(Channel channel) => Data.TryGetValue(channel, out var values) ? values : null;
}
