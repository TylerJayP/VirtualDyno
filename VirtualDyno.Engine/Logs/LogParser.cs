using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VirtualDyno.Engine.Logs;

/// <summary>
/// Reads CSV datalogs (AccessPort, and most other loggers that export CSV) and maps their columns to
/// <see cref="Channel"/>s in canonical units.
/// <para>
/// Column names differ between platforms ("RPM (RPM)", "Engine Speed", "Engine RPM"), so every header
/// is scored against each channel's known names. Exact names beat prefixes, prefixes beat whole-word
/// matches, and exclusion words stop near misses: "Boost Air Temp." is a temperature, not boost, and
/// "MAF Voltage" is not airflow. Units come from the header's trailing "(unit)" and are converted.
/// </para>
/// </summary>
public static partial class LogParser
{
    private sealed record Rule(Channel Channel, string[] Names, string[] Excludes);

    // Order inside Names matters: earlier names win ties.
    private static readonly Rule[] Rules =
    [
        new(Channel.Time, ["time", "time sec", "time s", "timestamp", "elapsed time", "log time", "seconds", "offset"],
            ["inj", "injector", "dwell", "pulse", "spark", "open", "close", "fan", "ignition", "cycle", "date", "clock", "boost"]),
        new(Channel.Rpm, ["rpm", "engine speed", "engine rpm", "engine speed rpm", "rpm engine"],
            ["target", "limit", "desired", "idle", "turbo", "fan", "pump", "wheel", "vehicle", "limiter"]),
        new(Channel.Speed, ["vehicle speed", "speed", "vss", "vehicle speed sensor", "ground speed", "gps speed", "wheel speed", "road speed"],
            ["engine", "fan", "turbo", "idle", "pump", "limit", "target", "shaft", "slip", "fl", "fr", "rl", "rr", "front", "rear", "left", "right", "desired", "wind"]),
        new(Channel.Pedal, ["accel pedal pos", "accelerator pedal position", "accel pedal position", "accelerator pedal", "pedal position", "accelerator position", "accel pedal", "pedal", "app"],
            ["target", "desired", "voltage", "volts", "sensor 2", "2"]),
        new(Channel.Throttle, ["throttle position", "throttle", "tps", "throttle pos", "throttle angle", "throttle opening", "throttle plate"],
            ["target", "desired", "pedal", "duty", "motor", "voltage", "volts", "request", "accel"]),
        new(Channel.Boost, ["boost", "boost pressure", "boost psi", "manifold pressure", "map", "manifold absolute pressure", "intake manifold pressure", "manifold abs pressure", "manifold air pressure"],
            ["temp", "temperature", "target", "duty", "error", "limit", "desired", "solenoid", "wastegate", "comp", "control", "request", "sensor voltage", "voltage", "volts", "barometric", "baro", "reference", "max", "offset", "air temp"]),
        new(Channel.Afr, ["actual afr", "afr", "air fuel ratio", "air fuel", "wideband afr", "wb afr", "a f ratio", "afr bank 1", "wideband", "lambda", "wideband lambda", "lambda bank 1"],
            ["target", "commanded", "learn", "correction", "desired", "trim", "error", "stoich", "command", "base", "request", "narrowband", "sensor 2", "voltage", "volts", "ltft", "stft"]),
        new(Channel.Maf, ["mass airflow", "maf", "mass air flow", "maf airflow", "mass airflow g s", "airflow", "air flow", "maf g s"],
            ["voltage", "volts", "frequency", "freq", "hz", "target", "per cyl", "per cylinder", "per rev", "g rev", "calc", "calculated", "limit", "correction"]),
        new(Channel.IntakeTemp, ["intake temp", "intake air temp", "intake air temperature", "iat", "ambient temp", "ambient air temp", "ambient temperature", "outside air temp", "air temp", "intake temperature"],
            ["boost", "charge", "manifold", "post", "intercooler", "target", "coolant", "oil", "fuel", "trans", "cat", "exhaust", "egt", "valve", "cam"]),
        new(Channel.ChargeTemp, ["boost air temp", "charge air temp", "manifold air temp", "post intercooler temp", "charge temp", "mat", "boost air temperature", "intercooler outlet temp", "charge air temperature"],
            ["target", "coolant", "oil", "fuel", "trans"]),
        new(Channel.KnockRetard, ["knock retard", "feedback knock", "knock correction", "total knock retard", "knock", "knock retard cyl 1", "ignition correction", "timing retard", "knock retard total"],
            ["learn", "count", "sensor", "voltage", "volts", "threshold", "fine knock", "dam", "noise", "limit", "detected", "window", "flag", "level"]),
        new(Channel.SparkAdvance, ["spark adv", "spark advance", "ignition timing", "timing advance", "ignition advance", "ign timing", "spark", "timing", "ignition angle"],
            ["retard", "knock", "target", "base", "correction", "learn", "inj", "injector", "cam", "valve", "dwell", "limit", "desired", "plug"]),
        new(Channel.Load, ["calculated load", "engine load", "load", "calc load", "absolute load", "load abs", "engine load absolute"],
            ["target", "limit", "desired", "request", "max", "error"]),
        new(Channel.Baro, ["barometric pressure", "baro", "atmospheric pressure", "ambient pressure", "baro pressure", "barometer"],
            ["target", "voltage", "volts"]),
        new(Channel.Gear, ["gear", "gear position", "current gear", "calculated gear", "gear engaged", "gear selected"],
            ["ratio", "shift", "target", "desired", "time", "pump"]),
    ];

    /// <summary>Parse a log from a stream (UTF-8, falling back to Windows-1252/Latin-1 for older loggers).</summary>
    public static ParsedLog Parse(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return Parse(Decode(buffer.ToArray()));
    }

    /// <summary>Decode bytes as UTF-8, or Latin-1 if they are not valid UTF-8 (AccessPort writes "°" in Latin-1).</summary>
    public static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static ParsedLog Parse(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Where(l => l.Trim().Length > 0)
            .ToList();
        if (lines.Count < 2) throw new LogFormatException("The file is empty or has no data rows.");

        char delimiter = DetectDelimiter(lines[0]);
        int headerIndex = FindHeaderRow(lines, delimiter);
        var header = SplitRow(lines[headerIndex], delimiter).Select(h => h.Trim()).ToList();

        var rows = new List<string[]>();
        for (int i = headerIndex + 1; i < lines.Count; i++)
        {
            var cells = SplitRow(lines[i], delimiter);
            if (cells.Count == 0) continue;
            rows.Add(cells.ToArray());
        }
        if (rows.Count == 0) throw new LogFormatException("The file has a header but no data rows.");

        var raw = new double[header.Count][];
        for (int c = 0; c < header.Count; c++)
        {
            raw[c] = new double[rows.Count];
            for (int r = 0; r < rows.Count; r++)
            {
                raw[c][r] = c < rows[r].Length ? ParseNumber(rows[r][c]) : double.NaN;
            }
        }

        var source = ReadSource(header);
        var (data, matches) = MapChannels(header, raw);

        return new ParsedLog
        {
            Columns = header,
            Data = data,
            Matches = matches,
            Source = source,
            RowCount = rows.Count,
        };
    }

    private static (Dictionary<Channel, double[]> Data, List<ChannelMatch> Matches) MapChannels(List<string> header, double[][] raw)
    {
        var candidates = new List<(int Score, Channel Channel, int Column)>();
        for (int c = 0; c < header.Count; c++)
        {
            if (header[c].StartsWith("AP Info", StringComparison.OrdinalIgnoreCase)) continue;
            if (raw[c].All(double.IsNaN)) continue;

            var (name, unit) = SplitHeader(header[c]);
            if (name.Length == 0) continue;
            string unitWords = Normalize(unit ?? string.Empty);

            foreach (var rule in Rules)
            {
                if (rule.Excludes.Any(x => ContainsWords(name, x))) continue;
                if (rule.Channel == Channel.Maf && unitWords is "v" or "volts" or "hz" or "mv") continue;
                if (rule.Channel is Channel.Speed && unitWords is "rpm") continue;

                int score = Score(name, rule.Names);
                if (score > 0) candidates.Add((score, rule.Channel, c));
            }
        }

        var data = new Dictionary<Channel, double[]>();
        var matches = new List<ChannelMatch>();
        var usedColumns = new HashSet<int>();

        foreach (var (_, channel, column) in candidates.OrderByDescending(x => x.Score).ThenBy(x => x.Column))
        {
            if (usedColumns.Contains(column)) continue;
            if (data.ContainsKey(channel)) continue;
            // Boost and absolute MAP compete for the same slot.
            if (channel == Channel.Boost && (data.ContainsKey(Channel.ManifoldAbsolute))) continue;

            var (_, unit) = SplitHeader(header[column]);
            var converted = Convert(channel, header[column], unit, raw[column], out var actualChannel, out var note);
            if (converted is null) continue;
            if (data.ContainsKey(actualChannel)) continue;

            data[actualChannel] = converted;
            usedColumns.Add(column);
            matches.Add(new ChannelMatch(actualChannel, header[column], unit, note));
        }

        return (data, matches.OrderBy(m => m.Channel).ToList());
    }

    private static double[]? Convert(Channel channel, string header, string? unit, double[] values, out Channel actual, out string? note)
    {
        actual = channel;
        note = null;
        string u = Normalize(unit ?? string.Empty);
        string uLetters = new string((unit ?? string.Empty).ToLowerInvariant().Where(char.IsLetter).ToArray());
        string name = SplitHeader(header).Name;

        switch (channel)
        {
            case Channel.Time:
            {
                if (u is "ms" or "msec" or "millis" or "milliseconds")
                {
                    note = "milliseconds → seconds";
                    return Scale(values, 0.001);
                }
                double step = MedianPositiveStep(values);
                if (unit is null && step > 5)
                {
                    note = "assumed milliseconds → seconds";
                    return Scale(values, 0.001);
                }
                return values;
            }

            case Channel.Speed:
                if (u.Contains("km") || u.Contains("kph") || u.Contains("kmh"))
                {
                    note = "km/h → mph";
                    return Scale(values, 0.621371);
                }
                return values;

            case Channel.IntakeTemp:
            case Channel.ChargeTemp:
                if (uLetters is "c" or "degc" or "celsius" or "degreesc")
                {
                    note = "°C → °F";
                    return values.Select(v => double.IsNaN(v) ? v : Units.CToF(v)).ToArray();
                }
                return values;

            case Channel.KnockRetard:
                // Some platforms log retard as negative degrees (Subaru feedback knock); store degrees pulled.
                return values.Select(v => double.IsNaN(v) ? v : Math.Abs(v)).ToArray();

            case Channel.Afr:
            {
                double median = Median(values);
                if (u.Contains("lambda") || name.Contains("lambda") || (median > 0.5 && median < 1.6))
                {
                    note = "lambda × 14.7 → AFR";
                    return Scale(values, 14.7);
                }
                return values;
            }

            case Channel.Maf:
                if (u.Contains("lb") && u.Contains("min"))
                {
                    note = "lb/min → g/s";
                    return Scale(values, Units.GramsPerPound / 60.0);
                }
                if (u.Contains("kg") && (u.Contains("h") || u.Contains("hr")))
                {
                    note = "kg/h → g/s";
                    return Scale(values, 1 / 3.6);
                }
                return values;

            case Channel.Baro:
                if (u.Contains("kpa")) { note = "kPa → inHg"; return Scale(values, 1 / 3.386389); }
                if (u.Contains("hpa") || u.Contains("mbar")) { note = "hPa → inHg"; return Scale(values, 1 / 33.86389); }
                if (u == "bar") { note = "bar → inHg"; return Scale(values, 29.52998); }
                if (u.Contains("psi")) { note = "psi → inHg"; return Scale(values, 1 / Units.PsiPerInHg); }
                {
                    // No unit: guess from magnitude.
                    double median = Median(values);
                    if (median > 60) { note = "assumed kPa → inHg"; return Scale(values, 1 / 3.386389); }
                    if (median < 20) { note = "assumed psi → inHg"; return Scale(values, 1 / Units.PsiPerInHg); }
                    return values;
                }

            case Channel.Boost:
                return ConvertPressure(name, u, values, out actual, out note);

            default:
                return values;
        }
    }

    /// <summary>Boost may be logged as gauge (boost) or absolute (MAP), in psi, kPa or bar.</summary>
    private static double[] ConvertPressure(string name, string u, double[] values, out Channel actual, out string? note)
    {
        bool saysAbsolute = ContainsWords(name, "absolute") || name == "map" || ContainsWords(name, "manifold absolute pressure")
                            || u.Contains("psia") || u.Contains("abs");
        bool saysGauge = ContainsWords(name, "boost") || ContainsWords(name, "relative") || ContainsWords(name, "gauge") || u.Contains("psig");
        double min = values.Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Min();
        double median = Median(values);

        double toPsi;
        string unitLabel;
        if (u.Contains("kpa")) { toPsi = Units.PsiPerKpa; unitLabel = "kPa"; }
        else if (u == "bar" || u.StartsWith("bar ")) { toPsi = 14.5038; unitLabel = "bar"; }
        else { toPsi = 1.0; unitLabel = "psi"; }

        bool isGauge;
        if (min < -0.5) isGauge = true;                 // vacuum shows up as negative only on a gauge reading
        else if (saysAbsolute && !saysGauge) isGauge = false;
        else if (saysGauge) isGauge = true;
        else isGauge = median * toPsi < 12;            // absolute MAP rarely sits below ~12 psia under load

        actual = isGauge ? Channel.Boost : Channel.ManifoldAbsolute;
        note = isGauge
            ? (toPsi == 1.0 ? null : $"{unitLabel} → psi")
            : $"absolute {unitLabel} → boost using barometric pressure";
        return Scale(values, toPsi);
    }

    private static LogSource ReadSource(List<string> header)
    {
        var info = header.FirstOrDefault(h => h.StartsWith("AP Info", StringComparison.OrdinalIgnoreCase));
        if (info is null) return new LogSource(null, null, null, null);

        var groups = BracketGroups().Matches(info).Select(m => m.Groups[1].Value.Trim()).ToList();
        string? device = null, firmware = null, vehicle = null, map = null;
        if (groups.Count > 0)
        {
            var parts = groups[0].Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            device = parts.ElementAtOrDefault(0);
            firmware = parts.ElementAtOrDefault(1);
        }
        foreach (var g in groups.Skip(1))
        {
            int colon = g.IndexOf(':');
            if (colon > 0 && g[..colon].Trim().ToLowerInvariant() is "reflash" or "map" or "ots" or "custom map")
            {
                map ??= g[(colon + 1)..].Trim();
            }
            else
            {
                vehicle ??= g;
            }
        }
        return new LogSource(device ?? "AccessPort", firmware, vehicle, map);
    }

    // ---- header helpers -------------------------------------------------------------------------

    /// <summary>Split "Boost Air Temp. (F)" into ("boost air temp", "F").</summary>
    internal static (string Name, string? Unit) SplitHeader(string header)
    {
        string h = header.Trim().Trim('"').Trim();
        string? unit = null;
        foreach (var (open, close) in new[] { ('(', ')'), ('[', ']') })
        {
            if (h.EndsWith(close))
            {
                int start = h.LastIndexOf(open);
                if (start > 0)
                {
                    unit = h[(start + 1)..^1].Trim();
                    h = h[..start];
                    break;
                }
            }
        }
        return (Normalize(h), string.IsNullOrWhiteSpace(unit) ? null : unit);
    }

    /// <summary>Lowercase, punctuation to spaces, single-spaced.</summary>
    internal static string Normalize(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool space = false;
        foreach (char ch in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                space = false;
            }
            else if (!space && sb.Length > 0)
            {
                sb.Append(' ');
                space = true;
            }
        }
        return sb.ToString().Trim();
    }

    private static bool ContainsWords(string name, string words) =>
        (" " + name + " ").Contains(" " + words + " ", StringComparison.Ordinal);

    private static int Score(string name, string[] patterns)
    {
        int best = 0;
        for (int i = 0; i < patterns.Length; i++)
        {
            string p = patterns[i];
            int s = 0;
            if (name == p) s = 1000 - i;
            else if (name.StartsWith(p + " ", StringComparison.Ordinal)) s = 600 - i;
            else if (ContainsWords(name, p)) s = 300 - i;
            best = Math.Max(best, s);
        }
        return best;
    }

    // ---- CSV helpers ----------------------------------------------------------------------------

    private static char DetectDelimiter(string line)
    {
        int commas = 0, tabs = 0, semis = 0;
        bool quoted = false;
        foreach (char c in line)
        {
            if (c == '"') quoted = !quoted;
            else if (!quoted && c == ',') commas++;
            else if (!quoted && c == '\t') tabs++;
            else if (!quoted && c == ';') semis++;
        }
        if (tabs > commas && tabs >= semis) return '\t';
        if (semis > commas) return ';';
        return ',';
    }

    private static int FindHeaderRow(List<string> lines, char delimiter)
    {
        int limit = Math.Min(lines.Count - 1, 60);
        for (int i = 0; i < limit; i++)
        {
            var cells = SplitRow(lines[i], delimiter);
            if (cells.Count < 2) continue;
            if (NumericFraction(cells) >= 0.5) continue;
            var next = SplitRow(lines[i + 1], delimiter);
            if (next.Count >= Math.Max(2, cells.Count / 2) && NumericFraction(next) >= 0.6) return i;
        }
        return 0;
    }

    private static double NumericFraction(List<string> cells)
    {
        int nonEmpty = 0, numeric = 0;
        foreach (var c in cells)
        {
            if (string.IsNullOrWhiteSpace(c)) continue;
            nonEmpty++;
            if (!double.IsNaN(ParseNumber(c))) numeric++;
        }
        return nonEmpty == 0 ? 0 : (double)numeric / nonEmpty;
    }

    internal static List<string> SplitRow(string line, char delimiter)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else quoted = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == delimiter) { cells.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString());
        return cells;
    }

    private static double ParseNumber(string cell)
    {
        var s = cell.Trim();
        if (s.Length == 0) return double.NaN;
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : double.NaN;
    }

    // ---- numeric helpers ------------------------------------------------------------------------

    private static double[] Scale(double[] values, double factor) =>
        values.Select(v => double.IsNaN(v) ? v : v * factor).ToArray();

    internal static double Median(IEnumerable<double> values)
    {
        var sorted = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return double.NaN;
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static double MedianPositiveStep(double[] values)
    {
        var steps = new List<double>();
        for (int i = 1; i < values.Length; i++)
        {
            double d = values[i] - values[i - 1];
            if (d > 0 && !double.IsNaN(d)) steps.Add(d);
        }
        return steps.Count == 0 ? double.NaN : Median(steps);
    }

    [GeneratedRegex(@"\[([^\]]*)\]")]
    private static partial Regex BracketGroups();
}

/// <summary>The file could not be read as a datalog.</summary>
public sealed class LogFormatException(string message) : Exception(message);
