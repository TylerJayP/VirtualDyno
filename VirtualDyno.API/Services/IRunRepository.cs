using VirtualDyno.API.Data;

namespace VirtualDyno.API.Services;

/// <summary>Persistence for saved runs. SQL Server (EF Core) in normal use; in-memory for tests and demos.</summary>
public interface IRunRepository
{
    /// <summary>All runs, newest first, without their log text.</summary>
    Task<IReadOnlyList<DynoRunEntity>> ListAsync(CancellationToken ct = default);

    Task<DynoRunEntity?> GetAsync(int id, CancellationToken ct = default);

    Task<DynoRunEntity> AddAsync(DynoRunEntity run, CancellationToken ct = default);

    /// <summary>Returns false if the run no longer exists.</summary>
    Task<bool> UpdateAsync(DynoRunEntity run, CancellationToken ct = default);

    /// <summary>Returns false if the run didn't exist.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
