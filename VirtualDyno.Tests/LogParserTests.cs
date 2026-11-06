using VirtualDyno.Engine.Logs;

namespace VirtualDyno.Tests;

public class LogParserTests
{
    [Fact]
    public void Ignores_near_miss_columns()
    {
        const string csv = "Time (sec),RPM (RPM),MAF Voltage (V),Mass Airflow (g/s),Boost Air Temp. (F),Boost (psi),Target Boost (psi),Intake Valve Adv. (°),Spark Adv. (°)\n" +
                           "0,3000,3.1,120,95,10,12,20,8\n0.05,3050,3.2,125,95,10.5,12,20,8\n";

        var log = LogParser.Parse(csv);

        Assert.Equal("Mass Airflow (g/s)", Column(log, Channel.Maf));
        Assert.Equal("Boost (psi)", Column(log, Channel.Boost));
        Assert.Equal("Boost Air Temp. (F)", Column(log, Channel.ChargeTemp));
        Assert.Equal("Spark Adv. (°)", Column(log, Channel.SparkAdvance));
    }

    [Fact]
    public void Converts_metric_units()
    {
        const string csv = "Time (ms),Engine Speed (rpm),Vehicle Speed (km/h),Intake Air Temperature (°C),Lambda,Barometric Pressure (kPa)\n" +
                           "0,3000,100,20,0.80,101.3\n50,3050,101,20,0.80,101.3\n";

        var log = LogParser.Parse(csv);

        Assert.Equal(0.05, log[Channel.Time][1], 1e-9);
        Assert.Equal(62.1371, log[Channel.Speed][0], 1e-3);
        Assert.Equal(68, log[Channel.IntakeTemp][0], 1e-9);
        Assert.Equal(11.76, log[Channel.Afr][0], 1e-9);
        Assert.Equal(29.91, log[Channel.Baro][0], 0.01);
    }

    [Fact]
    public void Treats_kpa_map_as_absolute_pressure()
    {
        const string csv = "Time,RPM,MAP (kPa)\n0,3000,180\n0.05,3050,182\n";

        var log = LogParser.Parse(csv);

        Assert.False(log.Has(Channel.Boost));
        Assert.Equal(180 * 0.145038, log[Channel.ManifoldAbsolute][0], 1e-6);
    }

    [Fact]
    public void Finds_the_header_below_metadata_lines_and_reads_quoted_cells()
    {
        const string csv = "Logger export v2\nVehicle: test car\n" +
                           "\"Time (sec)\",\"RPM\",\"Notes\"\n" +
                           "0,3000,\"a, b\"\n0.05,3050,x\n";

        var log = LogParser.Parse(csv);

        Assert.Equal(2, log.RowCount);
        Assert.Equal(3050, log[Channel.Rpm][1]);
    }

    [Fact]
    public void Reads_latin1_files()
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes("Time (sec),RPM (RPM),Knock Retard (°)\n0,3000,-1.5\n0.05,3050,0\n");

        var log = LogParser.Parse(new MemoryStream(bytes));

        Assert.Equal("Knock Retard (°)", Column(log, Channel.KnockRetard));
        Assert.Equal(1.5, log[Channel.KnockRetard][0]);
    }

    [Fact]
    public void Rejects_files_with_no_rows()
    {
        Assert.Throws<LogFormatException>(() => LogParser.Parse("Time,RPM\n"));
    }

    private static string Column(ParsedLog log, Channel channel) => log.Matches.Single(m => m.Channel == channel).Column;
}
