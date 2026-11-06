using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Logs;
using VirtualDyno.Engine.Numerics;
using VirtualDyno.Engine.Vehicles;

namespace VirtualDyno.Tests;

public class AtmosphereTests
{
    [Fact]
    public void Sae_factor_is_one_at_sae_reference_conditions()
    {
        // J1349 reference: 99 kPa dry air at 25 °C.
        double baro = 99000 / Units.PaPerInHg;
        Assert.Equal(1.0, Atmosphere.SaeJ1349(baro, 77, 0), 0.002);
    }

    [Fact]
    public void Std_factor_is_one_at_sea_level_60f()
    {
        Assert.Equal(1.0, Atmosphere.SaeJ607(29.92, 60), 1e-9);
    }

    [Fact]
    public void Salt_lake_altitude_corrects_up_about_seventeen_percent()
    {
        double baro = Atmosphere.BaroFromAltitudeInHg(4300);
        Assert.Equal(25.55, baro, 0.05);
        Assert.InRange(Atmosphere.SaeJ1349(baro, 75, 25), 1.15, 1.20);
    }

    [Fact]
    public void Dry_sea_level_air_at_59f_is_standard_density()
    {
        Assert.Equal(1.225, Atmosphere.AirDensity(29.921, 59, 0), 0.002);
    }
}

public class PSplineTests
{
    [Fact]
    public void Recovers_the_slope_of_a_noise_free_curve()
    {
        var x = Enumerable.Range(0, 120).Select(i => i * 0.05 + 0.003 * (i % 3)).ToArray();
        var y = x.Select(t => 2500 + 400 * t + 30 * t * t).ToArray();

        var fit = PSpline.FitAuto(x, y);

        for (int i = 5; i < x.Length - 5; i++)
        {
            Assert.Equal(400 + 60 * x[i], fit.Derivative[i], 0.5);
        }
    }

    [Fact]
    public void Smooths_noise_without_flattening_the_trend()
    {
        var rng = new Random(11);
        var x = Enumerable.Range(0, 150).Select(i => i * 0.043).ToArray();
        var y = x.Select(t => 2500 + 600 * t + 5 * Math.Sin(t) + (rng.NextDouble() - 0.5) * 30).ToArray();

        var fit = PSpline.FitAuto(x, y);

        double meanSlope = fit.Derivative.Skip(10).Take(130).Average();
        Assert.Equal(600, meanSlope, 15);
        Assert.True(fit.EffectiveDegreesOfFreedom < 20, "Noise should be smoothed, not interpolated");
    }

    [Fact]
    public void Rejects_unsorted_input()
    {
        Assert.Throws<ArgumentException>(() => PSpline.FitAuto([0, 2, 1, 3, 4, 5, 6, 7], [0, 1, 2, 3, 4, 5, 6, 7]));
    }
}

public class PullDetectorTests
{
    [Fact]
    public void Splits_a_flat_foot_shift_into_two_pulls()
    {
        var rows = new List<string> { "Time,RPM,Accel. Pedal Pos. (%)" };
        double t = 0;
        for (double r = 3000; r <= 6000; r += 50, t += 0.05) rows.Add($"{t:0.000},{r},100");
        for (double r = 4300; r <= 6000; r += 25, t += 0.05) rows.Add($"{t:0.000},{r},100");

        var pulls = PullDetector.Find(LogParser.Parse(string.Join('\n', rows)));

        Assert.Equal(2, pulls.Count);
        Assert.Equal(6000, pulls[0].EndRpm);
        Assert.Equal(4300, pulls[1].StartRpm);
        Assert.Equal(0, PullDetector.Best(pulls)!.Index);
    }

    [Fact]
    public void Ignores_part_throttle_and_short_bursts()
    {
        var rows = new List<string> { "Time,RPM,Accel. Pedal Pos. (%)" };
        double t = 0;
        for (double r = 2000; r <= 4000; r += 20, t += 0.05) rows.Add($"{t:0.000},{r},40");
        for (double r = 4000; r <= 4400; r += 40, t += 0.05) rows.Add($"{t:0.000},{r},100");

        Assert.Empty(PullDetector.Find(LogParser.Parse(string.Join('\n', rows))));
    }
}

public class VehicleCatalogTests
{
    [Theory]
    [InlineData("USDM 2011-2013 MAZDASPEED3 (Fed.)", "mazdaspeed3-gen2")]
    [InlineData("USDM 2007-2009 MAZDASPEED3", "mazdaspeed3-gen1")]
    [InlineData("USDM 2015-2021 Subaru WRX", "wrx-2015")]
    [InlineData("USDM 2015-2021 Subaru WRX STI", "wrx-sti-2015")]
    [InlineData("USDM 2016-2018 Ford Focus RS", "focus-rs-2016")]
    public void Matches_accessport_vehicle_descriptions(string description, string expectedKey)
    {
        Assert.Equal(expectedKey, VehicleCatalog.MatchLogVehicle(description)?.Key);
    }

    [Fact]
    public void Unknown_vehicles_do_not_match()
    {
        Assert.Null(VehicleCatalog.MatchLogVehicle("USDM 2020 Some Other Car"));
    }

    [Fact]
    public void Names_the_gear_from_rpm_per_mph()
    {
        var ms3 = VehicleCatalog.Find("mazdaspeed3-gen2")!;
        Assert.Equal(3, VehicleCatalog.DetectGear(ms3, 80.2)?.Gear);
        Assert.Equal(4, VehicleCatalog.DetectGear(ms3, 60.0)?.Gear);
        Assert.Null(VehicleCatalog.DetectGear(ms3, 70.0));
    }

    [Fact]
    public void Every_preset_has_sane_numbers()
    {
        Assert.All(VehicleCatalog.All, p =>
        {
            Assert.InRange(p.CurbWeightLb, 2000, 5000);
            Assert.InRange(p.DragAreaM2, 0.5, 1.0);
            Assert.True(p.GearRatios.Length == 0 || p.HasGearing, $"{p.Key} has gear ratios but no final drive or tire");
            Assert.Equal(p.Key, VehicleCatalog.Find(p.Key)!.Key);
        });
        Assert.Equal(VehicleCatalog.All.Count, VehicleCatalog.All.Select(p => p.Key).Distinct().Count());
    }

    [Theory]
    [InlineData("225/40R18", 25.087)]
    [InlineData("P215/45ZR17", 24.618)]
    [InlineData("245/35-19", 25.752)]
    public void Reads_tire_sizes(string size, double diameter)
    {
        Assert.Equal(diameter, TireSize.DiameterInches(size)!.Value, 0.01);
    }
}
