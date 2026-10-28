using System.Globalization;
using System.Text;
using VirtualDyno.Engine;

namespace VirtualDyno.Tests;

/// <summary>
/// Drives a virtual car with a known wheel-torque curve down a flat road and writes the datalog an
/// AccessPort would have recorded: irregular ~23 Hz samples, timestamps that jitter against the real
/// sample instant, whole-number RPM and whole-mph speed. If the dyno can recover the curve we put in,
/// it measures power correctly.
/// </summary>
internal sealed class SimulatedCar
{
    public double RpmPerMph { get; init; } = 80.0;
    public double WeightLb { get; init; } = 3450;
    public double DragAreaM2 { get; init; } = 0.695;
    public double RollingResistance { get; init; } = 0.012;
    public double RotationalMassFactor { get; init; } = 1.03;
    public double AirDensity { get; init; } = 1.012;
    public double StartRpm { get; init; } = 2500;
    public double EndRpm { get; init; } = 6300;
    public double TimestampJitterSec { get; init; } = 0.010;

    /// <summary>Optional wheelspin: extra RPM (above what wheel speed implies) between two RPMs.</summary>
    public (double FromRpm, double ToRpm, double ExtraPct)? Wheelspin { get; init; }

    /// <summary>Turbo-shaped wheel torque: spools to a peak at 3,500 then tapers.</summary>
    public static double WheelTorque(double rpm) =>
        Interpolate(rpm, [2000, 2500, 3500, 4500, 6200, 7000], [150, 180, 300, 270, 180, 150]);

    public static double WheelHp(double rpm) => WheelTorque(rpm) * rpm / Units.HpTorqueConstant;

    public string ToCsv(int seed)
    {
        var rng = new Random(seed);
        double m = Units.LbToKg(WeightLb);
        double v = Units.MphToMs(StartRpm / RpmPerMph);
        const double dt = 0.001;

        var times = new List<double>();
        var speeds = new List<double>();
        double t = 0;
        while (v / Units.MetersPerSecondPerMph * RpmPerMph < EndRpm)
        {
            double rpm = v / Units.MetersPerSecondPerMph * RpmPerMph;
            double power = WheelHp(rpm) * Units.WattsPerHp;
            double force = power / v - 0.5 * AirDensity * DragAreaM2 * v * v - RollingResistance * m * Units.Gravity;
            times.Add(t);
            speeds.Add(v);
            v += force / (m * RotationalMassFactor) * dt;
            t += dt;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Time (sec),RPM (RPM),Vehicle Speed (mph),Accel. Pedal Pos. (%),Boost (psi)");
        double sampleT = 0;
        double lastLogged = double.NegativeInfinity;
        while (sampleT < times[^1] - 0.06)
        {
            int i = Math.Min((int)Math.Round(sampleT / dt), times.Count - 1);
            double mph = speeds[i] / Units.MetersPerSecondPerMph;
            double rpm = mph * RpmPerMph;
            if (Wheelspin is { } spin && rpm >= spin.FromRpm && rpm <= spin.ToRpm)
            {
                // Tires break loose, flare, then hook back up: a smooth bump in RPM over wheel speed.
                double phase = (rpm - spin.FromRpm) / (spin.ToRpm - spin.FromRpm);
                rpm *= 1 + spin.ExtraPct / 100.0 * Math.Sin(Math.PI * phase);
            }

            double logged = Math.Max(sampleT + Gaussian(rng) * TimestampJitterSec, lastLogged + 0.001);
            lastLogged = logged;
            sb.Append(logged.ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
              .Append(Math.Round(rpm).ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Math.Round(mph).ToString(CultureInfo.InvariantCulture)).Append(",100,15")
              .AppendLine();
            sampleT += 0.028 + rng.NextDouble() * 0.029;
        }
        return sb.ToString();
    }

    private static double Gaussian(Random rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    private static double Interpolate(double x, double[] xs, double[] ys)
    {
        if (x <= xs[0]) return ys[0];
        for (int i = 1; i < xs.Length; i++)
        {
            if (x <= xs[i]) return ys[i - 1] + (ys[i] - ys[i - 1]) * (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        }
        return ys[^1];
    }
}
