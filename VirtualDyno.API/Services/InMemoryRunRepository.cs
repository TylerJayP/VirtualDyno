using System.Collections.Concurrent;
using VirtualDyno.API.Data;

namespace VirtualDyno.API.Services;

/// <summary>Keeps runs in memory. Used by the tests and by "Storage:Provider": "InMemory" for a zero-setup demo.</summary>
public sealed class InMemoryRunRepository : IRunRepository
{
    private readonly ConcurrentDictionary<int, DynoRunEntity> _runs = new();
    private int _nextId;

    public Task<IReadOnlyList<DynoRunEntity>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<DynoRunEntity>>(_runs.Values
            .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            .Select(r => Copy(r, includeLog: false))
            .ToList());

    public Task<DynoRunEntity?> GetAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_runs.TryGetValue(id, out var run) ? Copy(run, includeLog: true) : null);

    public Task<DynoRunEntity> AddAsync(DynoRunEntity run, CancellationToken ct = default)
    {
        var stored = Copy(run, includeLog: true);
        stored.Id = Interlocked.Increment(ref _nextId);
        _runs[stored.Id] = stored;
        return Task.FromResult(Copy(stored, includeLog: true));
    }

    public Task<bool> UpdateAsync(DynoRunEntity run, CancellationToken ct = default)
    {
        if (!_runs.ContainsKey(run.Id)) return Task.FromResult(false);
        _runs[run.Id] = Copy(run, includeLog: true);
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_runs.TryRemove(id, out _));

    private static DynoRunEntity Copy(DynoRunEntity r, bool includeLog) => new()
    {
        Id = r.Id,
        Name = r.Name,
        FileName = r.FileName,
        Notes = r.Notes,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
        LogText = includeLog ? r.LogText : string.Empty,
        SettingsJson = r.SettingsJson,
        VehicleKey = r.VehicleKey,
        VehicleName = r.VehicleName,
        Gear = r.Gear,
        WeightLb = r.WeightLb,
        Correction = r.Correction,
        PeakWhp = r.PeakWhp,
        PeakWhpRpm = r.PeakWhpRpm,
        PeakWtq = r.PeakWtq,
        PeakWtqRpm = r.PeakWtqRpm,
        PeakBoostPsi = r.PeakBoostPsi,
    };
}
