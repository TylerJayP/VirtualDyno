using Microsoft.EntityFrameworkCore;
using VirtualDyno.API.Services;

namespace VirtualDyno.API.Data;

/// <summary>SQL Server storage through EF Core.</summary>
public sealed class EfRunRepository(DynoDbContext db) : IRunRepository
{
    public async Task<IReadOnlyList<DynoRunEntity>> ListAsync(CancellationToken ct = default) =>
        await db.Runs.AsNoTracking()
            .OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id)
            // Project without LogText: the list never needs the (large) log.
            .Select(r => new DynoRunEntity
            {
                Id = r.Id,
                Name = r.Name,
                FileName = r.FileName,
                Notes = r.Notes,
                CreatedAtUtc = r.CreatedAtUtc,
                UpdatedAtUtc = r.UpdatedAtUtc,
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
            })
            .ToListAsync(ct);

    public Task<DynoRunEntity?> GetAsync(int id, CancellationToken ct = default) =>
        db.Runs.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<DynoRunEntity> AddAsync(DynoRunEntity run, CancellationToken ct = default)
    {
        db.Runs.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<bool> UpdateAsync(DynoRunEntity run, CancellationToken ct = default)
    {
        var existing = await db.Runs.FirstOrDefaultAsync(r => r.Id == run.Id, ct);
        if (existing is null) return false;
        db.Entry(existing).CurrentValues.SetValues(run);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default) =>
        await db.Runs.Where(r => r.Id == id).ExecuteDeleteAsync(ct) > 0;
}
