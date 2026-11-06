using Microsoft.EntityFrameworkCore;
using VirtualDyno.API.Services;

namespace VirtualDyno.API.Data;

public static class StorageExtensions
{
    /// <summary>"Storage:Provider" = "SqlServer" (default) or "InMemory" (no database; runs vanish on restart).</summary>
    public static IServiceCollection AddDynoStorage(this IServiceCollection services, IConfiguration config)
    {
        string provider = config["Storage:Provider"] ?? "SqlServer";
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRunRepository, InMemoryRunRepository>();
            return services;
        }

        string connection = config.GetConnectionString("Dyno")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Dyno in appsettings.json.");
        services.AddDbContext<DynoDbContext>(o => o.UseSqlServer(connection));
        services.AddScoped<IRunRepository, EfRunRepository>();
        return services;
    }

    /// <summary>Apply EF Core migrations at startup (SQL Server only).</summary>
    public static void InitializeStorage(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetService<DynoDbContext>();
        if (db is null) return;

        if (!db.Database.GetMigrations().Any())
        {
            throw new InvalidOperationException(
                "The database has no migrations yet. From the repo root run:\n" +
                "    dotnet tool restore\n" +
                "    dotnet ef migrations add InitialCreate --project VirtualDyno.API\n" +
                "then start the API again. (Or set \"Storage:Provider\" to \"InMemory\" to run without a database.)");
        }
        db.Database.Migrate();
    }
}
