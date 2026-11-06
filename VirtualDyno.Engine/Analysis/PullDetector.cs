using VirtualDyno.Engine.Logs;

namespace VirtualDyno.Engine.Analysis;

/// <summary>A wide-open-throttle pull: sample indices into the log, inclusive.</summary>
public sealed record Pull(int Index, int Start, int End, double StartTime, double EndTime, double StartRpm, double EndRpm)
{
    public double Duration => EndTime - StartTime;
    public double RpmSpan => EndRpm - StartRpm;
    public int SampleCount => End - Start + 1;
}

/// <summary>
/// Finds the stretches of a log where the driver was flat out and RPM was climbing.
/// <para>
/// A pull starts when the pedal (or throttle, if there's no pedal channel) goes wide open and ends at
/// the highest RPM before the driver lifts or shifts. Flat-foot shifts keep the pedal down, so a drop of
/// more than a few hundred RPM also ends a pull and starts the next one.
/// </para>
/// </summary>
public static class PullDetector
{
    public const double MinDurationSec = 1.5;
    public const double MinRpmSpan = 1000;
    public const int MinSamples = 12;
    public const double ShuntWindowSec = 0.6;

    public static IReadOnlyList<Pull> Find(ParsedLog log)
    {
        var time = log[Channel.Time];
        var rpm = log[Channel.Rpm];
        int n = time.Length;

        var throttle = log.TryGet(Channel.Pedal) ?? log.TryGet(Channel.Throttle);
        bool[] wot = new bool[n];
        if (throttle is null)
        {
            Array.Fill(wot, true);
        }
        else
        {
            double top = Percentile(throttle, 0.98);
            double threshold = Math.Max(0.85 * top, 50);
            for (int i = 0; i < n; i++) wot[i] = throttle[i] >= threshold;
        }

        // Treat a single-sample dip in throttle as noise, not a lift.
        for (int i = 1; i < n - 1; i++)
        {
            if (!wot[i] && wot[i - 1] && wot[i + 1]) wot[i] = true;
        }

        var pulls = new List<Pull>();
        int idx = 0;
        while (idx < n)
        {
            if (!wot[idx] || !Valid(time[idx], rpm[idx])) { idx++; continue; }
            int segStart = idx;
            while (idx < n && wot[idx] && Valid(time[idx], rpm[idx])) idx++;
            int segEnd = idx - 1;
            foreach (var p in SplitAtShifts(time, rpm, segStart, segEnd)) pulls.Add(p);
        }

        return pulls
            .Where(p => p.Duration >= MinDurationSec && p.RpmSpan >= MinRpmSpan && p.SampleCount >= MinSamples)
            .Select((p, i) => p with { Index = i })
            .ToList();
    }

    /// <summary>The pull a dyno operator would pick: the widest RPM sweep, then the longest.</summary>
    public static Pull? Best(IReadOnlyList<Pull> pulls) =>
        pulls.OrderByDescending(p => p.RpmSpan).ThenByDescending(p => p.Duration).FirstOrDefault();

    private static IEnumerable<Pull> SplitAtShifts(double[] time, double[] rpm, int start, int end)
    {
        int s = start;
        double peak = rpm[s];
        for (int i = start + 1; i <= end; i++)
        {
            if (rpm[i] > peak) peak = rpm[i];
            // A real shift drops RPM by hundreds within a few tenths of a second.
            if (peak - rpm[i] > 300)
            {
                var p = Trim(time, rpm, s, i - 1);
                if (p is not null) yield return p;
                s = i;
                peak = rpm[i];
            }
        }
        var last = Trim(time, rpm, s, end);
        if (last is not null) yield return last;
    }

    private static Pull? Trim(double[] time, double[] rpm, int start, int end)
    {
        if (end - start < 2) return null;

        // End at the highest RPM: past that point the driver has lifted, shifted or hit the limiter.
        int top = start;
        for (int i = start; i <= end; i++)
        {
            if (rpm[i] > rpm[top]) top = i;
        }

        // Start once RPM is actually rising (skip the moment before the car responds).
        int first = start;
        while (first < top && rpm[first + 1] <= rpm[first]) first++;

        // Tip-in shunt: stomping the pedal winds up the axles and mounts, and RPM rocks back and forth
        // for a few tenths of a second before the steady climb. Start at the last low point inside the
        // first 0.6 s (a sample lower than the one before it) that RPM never returns below.
        var suffixMin = new double[top - first + 1];
        double running = double.PositiveInfinity;
        for (int i = top; i >= first; i--)
        {
            suffixMin[i - first] = running;
            running = Math.Min(running, rpm[i]);
        }
        int settled = first;
        for (int i = first; i < top && time[i] - time[first] <= ShuntWindowSec; i++)
        {
            bool dip = i > first && rpm[i] < rpm[i - 1];
            if (dip && rpm[i] <= suffixMin[i - first]) settled = i;
        }
        first = settled;

        if (top - first < 2) return null;
        return new Pull(0, first, top, time[first], time[top], rpm[first], rpm[top]);
    }

    private static bool Valid(double t, double r) => !double.IsNaN(t) && !double.IsNaN(r) && r > 0;

    internal static double Percentile(double[] values, double p)
    {
        var sorted = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToArray();
        if (sorted.Length == 0) return double.NaN;
        double pos = p * (sorted.Length - 1);
        int lo = (int)Math.Floor(pos);
        int hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
