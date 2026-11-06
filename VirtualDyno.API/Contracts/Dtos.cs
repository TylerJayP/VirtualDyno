using VirtualDyno.API.Data;
using VirtualDyno.Engine.Analysis;
using VirtualDyno.Engine.Vehicles;

namespace VirtualDyno.API.Contracts;

/// <summary>A car the user can pick. Mirrors the engine's preset without exposing its matching rules.</summary>
public sealed record VehicleDto(
    string Key,
    string Name,
    string Make,
    string Model,
    int YearFrom,
    int YearTo,
    int CurbWeightLb,
    Drivetrain Drive,
    bool Turbo,
    double DragCoefficient,
    double FrontalAreaM2,
    double DragAreaM2,
    int GearCount,
    string? StockTire,
    string? SpecNotes)
{
    public static VehicleDto From(VehiclePreset p) => new(
        p.Key, p.Name, p.Make, p.Model, p.YearFrom, p.YearTo, p.CurbWeightLb, p.Drive, p.Turbo,
        p.DragCoefficient, p.FrontalAreaM2, Math.Round(p.DragAreaM2, 3), p.GearRatios.Length, p.StockTire, p.SpecNotes);
}

/// <summary>One row in the saved-runs list.</summary>
public sealed record RunSummaryDto(
    int Id,
    string Name,
    string FileName,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? VehicleKey,
    string VehicleName,
    int? Gear,
    double WeightLb,
    string Correction,
    double PeakWhp,
    double PeakWhpRpm,
    double PeakWtq,
    double PeakWtqRpm,
    double? PeakBoostPsi)
{
    public static RunSummaryDto From(DynoRunEntity e) => new(
        e.Id, e.Name, e.FileName, e.Notes, e.CreatedAtUtc, e.UpdatedAtUtc, e.VehicleKey, e.VehicleName, e.Gear,
        e.WeightLb, e.Correction, e.PeakWhp, e.PeakWhpRpm, e.PeakWtq, e.PeakWtqRpm, e.PeakBoostPsi);
}

/// <summary>A saved run with the settings it was saved with and the curve computed from its log.</summary>
public sealed record RunDetailDto(RunSummaryDto Run, DynoSettings Settings, DynoResult Analysis);

/// <summary>Rename a run, edit notes, or change the settings it's computed with.</summary>
public sealed record UpdateRunRequest(string? Name, string? Notes, DynoSettings? Settings);
