using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Logs;

namespace VirtualDyno.Tests;

/// <summary>The real AccessPort log in SampleLogs: a 3rd-gear pull in a tuned 2012 Mazdaspeed3.</summary>
public class SampleLogTests
{
    private static ParsedLog Load() =>
        LogParser.Parse(File.OpenRead(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SampleLogs"), "mazdaspeed3*.csv").Single()));

    private static readonly DynoSettings Settings = new()
    {
        WeightLb = 3450,
        AltitudeFt = 4300,
        HumidityPct = 25,
        Correction = CorrectionMode.Uncorrected,
    };

    [Fact]
    public void Reads_the_accessport_header()
    {
        var log = Load();

        Assert.Equal("AP3-MAZ-002", log.Source.Device);
        Assert.Equal("USDM 2011-2013 MAZDASPEED3 (Fed.)", log.Source.Vehicle);
        Assert.Equal("T.Perkins-2012ots-v2-B.ptm", log.Source.Map);
    }

    [Fact]
    public void Maps_every_channel_the_dyno_uses()
    {
        var log = Load();

        foreach (var channel in new[] { Channel.Time, Channel.Rpm, Channel.Speed, Channel.Pedal, Channel.Boost,
                     Channel.Afr, Channel.Maf, Channel.IntakeTemp, Channel.ChargeTemp, Channel.KnockRetard, Channel.SparkAdvance })
        {
            Assert.True(log.Has(channel), $"{channel} should be mapped");
        }
        Assert.Equal("Boost (psi)", log.Matches.Single(m => m.Channel == Channel.Boost).Column);
        Assert.Equal("Boost Air Temp. (F)", log.Matches.Single(m => m.Channel == Channel.ChargeTemp).Column);
        Assert.Equal("Mass Airflow (g/s)", log.Matches.Single(m => m.Channel == Channel.Maf).Column);
    }

    [Fact]
    public void Identifies_the_car_and_the_gear()
    {
        var result = DynoAnalyzer.Analyze(Load(), Settings);

        Assert.Equal("mazdaspeed3-gen2", result.Vehicle.Key);
        Assert.True(result.Vehicle.AutoDetected);
        Assert.Equal(3, result.Gearing.DetectedGear);
        Assert.Equal(80.2, result.Gearing.RpmPerMph, 0.5);
        Assert.Equal(0, result.Gearing.SlipPoints);
    }

    [Fact]
    public void Finds_one_third_gear_pull_and_skips_the_tip_in()
    {
        var result = DynoAnalyzer.Analyze(Load(), Settings);

        var pull = Assert.Single(result.Pulls);
        Assert.InRange(pull.StartRpm, 2500, 2800);
        Assert.Equal(6168, pull.EndRpm);
    }

    [Fact]
    public void Produces_a_plausible_curve_that_the_fuel_burned_agrees_with()
    {
        var result = DynoAnalyzer.Analyze(Load(), Settings);

        Assert.InRange(result.Peaks.Whp, 205, 240);
        Assert.InRange(result.Peaks.WhpRpm, 4300, 5200);
        Assert.InRange(result.Peaks.Wtq, 265, 300);
        Assert.InRange(result.Peaks.WtqRpm, 3300, 3900);
        Assert.Equal(18.9, result.Peaks.BoostPsi!.Value, 0.1);

        Assert.NotNull(result.CrossCheck);
        Assert.True(result.CrossCheck!.Agrees);
        Assert.False(result.CrossCheck.AfrAssumed);
    }

    [Fact]
    public void Uses_the_logged_intake_temperature_for_ambient()
    {
        var result = DynoAnalyzer.Analyze(Load(), Settings);

        Assert.Equal(ValueSource.Log, result.Environment.AmbientSource);
        Assert.Equal(75, result.Environment.AmbientTempF, 1.0);
    }

    [Fact]
    public void Smoothing_bias_moves_the_peak_only_a_little()
    {
        var log = Load();
        var auto = DynoAnalyzer.Analyze(log, Settings);
        var less = DynoAnalyzer.Analyze(log, Settings with { SmoothingBias = -1 });
        var more = DynoAnalyzer.Analyze(log, Settings with { SmoothingBias = 1 });

        Assert.InRange(less.Peaks.Whp / auto.Peaks.Whp, 0.97, 1.04);
        Assert.InRange(more.Peaks.Whp / auto.Peaks.Whp, 0.96, 1.03);
    }
}
