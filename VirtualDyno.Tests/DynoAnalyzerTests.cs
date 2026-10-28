using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Logs;

namespace VirtualDyno.Tests;

public class DynoAnalyzerTests
{
    // Conditions that give the same air density the simulated car drove through (1.012 kg/m³).
    private static readonly DynoSettings SimulatedConditions = new()
    {
        WeightLb = 3450,
        DragAreaM2 = 0.695,
        AltitudeFt = 4300,
        AmbientTempF = 75,
        HumidityPct = 25,
        Correction = CorrectionMode.Uncorrected,
    };

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Recovers_the_power_curve_of_a_simulated_car(int seed)
    {
        var csv = new SimulatedCar().ToCsv(seed);

        var result = DynoAnalyzer.Analyze(LogParser.Parse(csv), SimulatedConditions);

        double truePeak = Enumerable.Range(2500, 3800).Max(r => SimulatedCar.WheelHp(r));
        Assert.InRange(result.Peaks.Whp / truePeak - 1, -0.02, 0.02);

        // Whole-curve accuracy away from the very ends of the pull.
        var inner = result.Points.Where(p => p.Rpm is > 2900 and < 6000).ToList();
        double rms = Math.Sqrt(inner.Average(p => Math.Pow(p.Whp / SimulatedCar.WheelHp(p.Rpm) - 1, 2)));
        Assert.True(rms < 0.015, $"Curve RMS error {rms:P2} should be under 1.5%");
    }

    [Fact]
    public void Same_log_gives_the_same_answer_every_time()
    {
        var log = LogParser.Parse(new SimulatedCar().ToCsv(7));

        var first = DynoAnalyzer.Analyze(log, SimulatedConditions);
        var second = DynoAnalyzer.Analyze(log, SimulatedConditions);

        Assert.Equal(first.Peaks, second.Peaks);
    }

    [Fact]
    public void Measures_the_rpm_per_mph_ratio_from_the_log()
    {
        var log = LogParser.Parse(new SimulatedCar { RpmPerMph = 72.5 }.ToCsv(3));

        var result = DynoAnalyzer.Analyze(log, SimulatedConditions);

        Assert.Equal(ValueSource.Measured, result.Gearing.Source);
        Assert.Equal(72.5, result.Gearing.RpmPerMph, 0.5);
    }

    [Fact]
    public void Horsepower_and_torque_cross_at_5252_rpm()
    {
        var result = DynoAnalyzer.Analyze(LogParser.Parse(new SimulatedCar().ToCsv(2)), SimulatedConditions);

        var near = result.Points.MinBy(p => Math.Abs(p.Rpm - 5252))!;
        Assert.Equal(near.Whp, near.Wtq, near.Whp * 0.01);
    }

    [Fact]
    public void Correction_scales_power_by_the_reported_factor()
    {
        var log = LogParser.Parse(new SimulatedCar().ToCsv(4));

        var raw = DynoAnalyzer.Analyze(log, SimulatedConditions);
        var sae = DynoAnalyzer.Analyze(log, SimulatedConditions with { Correction = CorrectionMode.Sae });

        Assert.True(sae.Environment.CorrectionFactor > 1.1, "Thin air at 4,300 ft should correct upward");
        Assert.Equal(raw.Peaks.Whp * sae.Environment.CorrectionFactor, sae.Peaks.Whp, 0.2);
        Assert.Equal(raw.Peaks.Whp, sae.Peaks.UncorrectedWhp, 0.01);
    }

    [Fact]
    public void Heavier_car_means_more_power_for_the_same_acceleration()
    {
        var log = LogParser.Parse(new SimulatedCar().ToCsv(5));

        var light = DynoAnalyzer.Analyze(log, SimulatedConditions);
        var heavy = DynoAnalyzer.Analyze(log, SimulatedConditions with { WeightLb = 3450 * 1.10 });

        // Nearly all of the force is accelerating the car, so +10% weight is close to +10% power.
        Assert.InRange(heavy.Peaks.Whp / light.Peaks.Whp, 1.08, 1.10);
    }

    [Fact]
    public void Wheelspin_is_flagged_and_left_off_the_peaks()
    {
        var car = new SimulatedCar { Wheelspin = (3300, 3900, 8) };
        var result = DynoAnalyzer.Analyze(LogParser.Parse(car.ToCsv(6)), SimulatedConditions);

        Assert.True(result.Gearing.SlipPoints > 0);
        Assert.Contains(result.Warnings, w => w.Code == "SLIP");
        Assert.InRange(result.Gearing.SlipFromRpm!.Value, 3200, 3700);
        Assert.InRange(result.Peaks.WtqRpm, 3900, 6300);
    }

    [Fact]
    public void Uses_gearing_when_the_log_has_no_vehicle_speed()
    {
        var csv = string.Join('\n', new SimulatedCar().ToCsv(1).Split('\n').Select(line =>
            string.Join(',', line.Split(',').Where((_, i) => i != 2))));
        var log = LogParser.Parse(csv);
        Assert.False(log.Has(Channel.Speed));

        Assert.Throws<DynoAnalysisException>(() => DynoAnalyzer.Analyze(log, SimulatedConditions));

        var withGear = DynoAnalyzer.Analyze(log, SimulatedConditions with { VehicleKey = "mazdaspeed3-gen2", Gear = 3 });
        Assert.Equal(ValueSource.Catalog, withGear.Gearing.Source);
        Assert.Contains(withGear.Warnings, w => w.Code == "NO_SPEED");
    }

    [Fact]
    public void A_log_without_a_full_throttle_pull_is_rejected_with_a_reason()
    {
        const string cruise = "Time (sec),RPM (RPM),Vehicle Speed (mph),Accel. Pedal Pos. (%)\n" +
                              "0.00,2500,40,20\n0.05,2502,40,20\n0.10,2499,40,20\n0.15,2501,40,21\n0.20,2500,40,20\n";

        var ex = Assert.Throws<DynoAnalysisException>(() => DynoAnalyzer.Analyze(LogParser.Parse(cruise)));
        Assert.Contains("full-throttle", ex.Message);
    }

    [Fact]
    public void Warns_when_weight_is_not_entered()
    {
        var result = DynoAnalyzer.Analyze(LogParser.Parse(new SimulatedCar().ToCsv(1)),
            SimulatedConditions with { WeightLb = null, VehicleKey = "mazdaspeed3-gen2" });

        Assert.Equal(ValueSource.Catalog, result.Vehicle.WeightSource);
        Assert.Equal(3272 + 200, result.Vehicle.WeightLb);
        Assert.Contains(result.Warnings, w => w.Code == "WEIGHT_DEFAULT");
    }
}
