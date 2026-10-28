using System.Text;
using Microsoft.AspNetCore.Mvc;
using VirtualDyno.API.Contracts;
using VirtualDyno.API.Services;
using VirtualDyno.Engine;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Vehicles;

namespace VirtualDyno.API.Controllers;

/// <summary>The built-in car list.</summary>
[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    [HttpGet]
    public ActionResult<IEnumerable<VehicleDto>> GetAll() =>
        Ok(VehicleCatalog.All.OrderBy(p => p.Make).ThenBy(p => p.Model).ThenByDescending(p => p.YearFrom).Select(VehicleDto.From));
}

/// <summary>Analyze a log without saving it, so settings can be tuned before a run is kept.</summary>
[ApiController]
[Route("api/analyze")]
public sealed class AnalyzeController(DynoService dyno) : ControllerBase
{
    /// <param name="file">The datalog (CSV).</param>
    /// <param name="settings">Optional <see cref="DynoSettings"/> as JSON.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DynoService.MaxLogBytes + 1_000_000)]
    [ProducesResponseType<DynoResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DynoResult>> Analyze(IFormFile? file, [FromForm] string? settings, CancellationToken ct)
    {
        var (_, text) = await DynoService.ReadUploadAsync(file, ct);
        return Ok(dyno.Analyze(text, DynoService.ReadSettings(settings)));
    }
}

/// <summary>Saved runs.</summary>
[ApiController]
[Route("api/runs")]
public sealed class RunsController(DynoService dyno) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RunSummaryDto>>> List(CancellationToken ct) =>
        Ok(await dyno.ListAsync(ct));

    /// <param name="id">Run id.</param>
    /// <param name="correction">View the run with a different correction standard (not saved).</param>
    /// <param name="smoothing">View the run with a different smoothing bias (not saved).</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType<RunDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunDetailDto>> Get(int id, [FromQuery] CorrectionMode? correction, [FromQuery] int? smoothing,
        CancellationToken ct)
    {
        var run = await dyno.GetAsync(id, correction, smoothing, ct);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DynoService.MaxLogBytes + 1_000_000)]
    [ProducesResponseType<RunDetailDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RunDetailDto>> Create(IFormFile? file, [FromForm] string? settings, [FromForm] string? name,
        [FromForm] string? notes, CancellationToken ct)
    {
        var (fileName, text) = await DynoService.ReadUploadAsync(file, ct);
        var saved = await dyno.SaveAsync(fileName, text, DynoService.ReadSettings(settings), name, notes, ct);
        return CreatedAtAction(nameof(Get), new { id = saved.Run.Id }, saved);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<RunDetailDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RunDetailDto>> Update(int id, UpdateRunRequest request, CancellationToken ct)
    {
        var run = await dyno.UpdateAsync(id, request, ct);
        return run is null ? NotFound() : Ok(run);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await dyno.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>Download the original log.</summary>
    [HttpGet("{id:int}/log")]
    public async Task<IActionResult> DownloadLog(int id, CancellationToken ct)
    {
        var log = await dyno.GetLogAsync(id, ct);
        return log is null ? NotFound() : File(Encoding.UTF8.GetBytes(log.Value.Text), "text/csv", log.Value.FileName);
    }
}
