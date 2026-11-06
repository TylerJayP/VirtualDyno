using Microsoft.Extensions.Logging.Abstractions;
using VirtualDyno.API.Contracts;
using VirtualDyno.API.Services;
using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;

namespace VirtualDyno.Tests;

public class DynoServiceTests
{
    private static string SampleLog() =>
        File.ReadAllText(Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "SampleLogs"), "mazdaspeed3*.csv").Single(),
            System.Text.Encoding.Latin1);

    private static DynoService NewService() => new(new InMemoryRunRepository(), NullLogger<DynoService>.Instance);

    private static readonly DynoSettings Settings = new() { WeightLb = 3450, AltitudeFt = 4300 };

    [Fact]
    public async Task Saves_a_run_and_lists_it_with_its_peaks()
    {
        var service = NewService();

        var saved = await service.SaveAsync("pull.csv", SampleLog(), Settings, "Baseline", "93 octane", default);
        var list = await service.ListAsync();

        var row = Assert.Single(list);
        Assert.Equal("Baseline", row.Name);
        Assert.Equal(3, row.Gear);
        Assert.Equal(saved.Analysis.Peaks.Whp, row.PeakWhp);
        Assert.Equal("mazdaspeed3-gen2", row.VehicleKey);
    }

    [Fact]
    public async Task Viewing_with_another_correction_does_not_change_what_is_saved()
    {
        var service = NewService();
        var saved = await service.SaveAsync("pull.csv", SampleLog(), Settings, null, null, default);

        var uncorrected = await service.GetAsync(saved.Run.Id, CorrectionMode.Uncorrected);

        Assert.Equal(saved.Analysis.Peaks.UncorrectedWhp, uncorrected!.Analysis.Peaks.Whp);
        Assert.Equal(CorrectionMode.Sae, uncorrected.Settings.Correction);
        Assert.Equal(saved.Run.PeakWhp, (await service.ListAsync())[0].PeakWhp);
    }

    [Fact]
    public async Task Changing_the_weight_recomputes_the_saved_peaks()
    {
        var service = NewService();
        var saved = await service.SaveAsync("pull.csv", SampleLog(), Settings, null, null, default);

        var updated = await service.UpdateAsync(saved.Run.Id, new UpdateRunRequest("Renamed", null, Settings with { WeightLb = 3600 }));

        Assert.Equal("Renamed", updated!.Run.Name);
        Assert.Equal(3600, updated.Run.WeightLb);
        Assert.True(updated.Run.PeakWhp > saved.Run.PeakWhp);
    }

    [Fact]
    public async Task A_log_that_cannot_be_analyzed_is_not_saved()
    {
        var service = NewService();

        await Assert.ThrowsAsync<DynoAnalysisException>(() =>
            service.SaveAsync("cruise.csv", "Time,RPM\n0,2000\n0.1,2001\n0.2,2002\n", Settings, null, null, default));
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task Missing_runs_return_null_and_delete_reports_false()
    {
        var service = NewService();

        Assert.Null(await service.GetAsync(42));
        Assert.Null(await service.UpdateAsync(42, new UpdateRunRequest("x", null, null)));
        Assert.False(await service.DeleteAsync(42));
    }

    [Fact]
    public void Settings_json_round_trips_and_bad_json_is_a_400()
    {
        var parsed = DynoService.ReadSettings("{\"weightLb\":3400,\"correction\":\"Std\",\"smoothingBias\":1}");

        Assert.Equal(3400, parsed.WeightLb);
        Assert.Equal(CorrectionMode.Std, parsed.Correction);
        Assert.Equal(1, parsed.SmoothingBias);
        Assert.Throws<InvalidUploadException>(() => DynoService.ReadSettings("{not json"));
    }
}
