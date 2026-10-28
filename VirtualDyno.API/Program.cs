using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using VirtualDyno.API.Data;
using VirtualDyno.API.Infrastructure;
using VirtualDyno.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DynoExceptionHandler>();
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = DynoService.MaxLogBytes + 1_000_000);

builder.Services.AddDynoStorage(builder.Configuration);   // SQL Server via EF Core, or in-memory
builder.Services.AddScoped<DynoService>();
builder.Services.AddApiDocs();

string[] origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) app.MapApiDocs();
app.UseCors();
app.MapControllers();

app.InitializeStorage();
app.Run();

/// <summary>Exposed for integration tests.</summary>
public partial class Program;
