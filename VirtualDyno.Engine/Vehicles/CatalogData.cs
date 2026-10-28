namespace VirtualDyno.Engine.Vehicles;

/// <summary>
/// Factory specs for AccessPort-supported cars.
/// <para>
/// Only curb weight and drag area (Cd × frontal area) change the power number, and weight should be
/// replaced by a scale reading anyway. Frontal area is rarely published, so it is estimated as
/// 0.84 × width × height (a standard approximation for passenger cars). Gear ratios are only used to
/// name the gear; presets without verified gearing simply don't label it.
/// </para>
/// </summary>
internal static class CatalogData
{
    private static double Area(double widthMm, double heightMm) => Math.Round(0.84 * widthMm / 1000 * heightMm / 1000, 3);

    public static readonly IReadOnlyList<VehiclePreset> Presets =
    [
        new VehiclePreset
        {
            Key = "mazdaspeed3-gen2", Make = "Mazda", Model = "Mazdaspeed3", YearFrom = 2010, YearTo = 2013,
            CurbWeightLb = 3272, Drive = Drivetrain.Fwd, DragCoefficient = 0.32, FrontalAreaM2 = Area(1770, 1460),
            GearRatios = [3.214, 1.913, 1.366, 1.025, 0.948, 0.790],
            FinalDrives = [4.187, 4.187, 4.187, 4.187, 3.526, 3.526],
            StockTire = "225/40R18",
            MatchTokens = ["MAZDASPEED3"],
            SpecNotes = "Mazda North America 2010 and 2011 MAZDASPEED3 spec sheets. Frontal area estimated.",
        },
        new VehiclePreset
        {
            Key = "mazdaspeed3-gen1", Make = "Mazda", Model = "Mazdaspeed3", YearFrom = 2007, YearTo = 2009,
            CurbWeightLb = 3153, Drive = Drivetrain.Fwd, DragCoefficient = 0.32, FrontalAreaM2 = Area(1770, 1460),
            MatchTokens = ["MAZDASPEED3"],
            SpecNotes = "Curb weight from iSeeCars (2008). Drag and frontal area estimated from the 2010–2013 car. Gearing not verified, so the gear isn't named.",
        },
        new VehiclePreset
        {
            Key = "mazdaspeed6", Make = "Mazda", Model = "Mazdaspeed6", YearFrom = 2006, YearTo = 2007,
            CurbWeightLb = 3589, Drive = Drivetrain.Awd, DragCoefficient = 0.31, FrontalAreaM2 = Area(1781, 1430),
            GearRatios = [3.54, 2.24, 1.54, 1.17, 1.09, 0.85],
            FinalDrives = [3.94],
            StockTire = "215/45R18",
            MatchTokens = ["MAZDASPEED6"],
            SpecNotes = "Mazda North America 2006 MAZDASPEED6 spec sheet. Drag coefficient and frontal area estimated.",
        },
        new VehiclePreset
        {
            Key = "wrx-2015", Make = "Subaru", Model = "WRX", YearFrom = 2015, YearTo = 2021,
            CurbWeightLb = 3294, Drive = Drivetrain.Awd, DragCoefficient = 0.34, FrontalAreaM2 = Area(1795, 1475),
            GearRatios = [3.454, 1.947, 1.296, 0.972, 0.780, 0.666],
            FinalDrives = [4.111],
            StockTire = "235/45R17",
            MatchTokens = ["WRX"], ExcludeTokens = ["STI"],
            SpecNotes = "Subaru Canada 2018 WRX specifications (6MT gearing, Cd, dimensions); curb weight from iSeeCars (2018). Frontal area estimated.",
        },
        new VehiclePreset
        {
            Key = "wrx-sti-2015", Make = "Subaru", Model = "WRX STI", YearFrom = 2015, YearTo = 2021,
            CurbWeightLb = 3446, Drive = Drivetrain.Awd, DragCoefficient = 0.34, FrontalAreaM2 = Area(1795, 1475),
            MatchTokens = ["STI"],
            SpecNotes = "Curb weight from iSeeCars (2018). Drag and frontal area estimated from the WRX body. Gearing not verified, so the gear isn't named.",
        },
        new VehiclePreset
        {
            Key = "focus-st-2013", Make = "Ford", Model = "Focus ST", YearFrom = 2013, YearTo = 2018,
            CurbWeightLb = 3214, Drive = Drivetrain.Fwd, DragCoefficient = 0.33, FrontalAreaM2 = Area(1823, 1472),
            MatchTokens = ["FOCUS", "ST"], ExcludeTokens = ["RS"],
            SpecNotes = "Curb weight and dimensions from auto123 (2015). Drag coefficient estimated. Gearing not verified, so the gear isn't named.",
        },
        new VehiclePreset
        {
            Key = "focus-rs-2016", Make = "Ford", Model = "Focus RS", YearFrom = 2016, YearTo = 2018,
            CurbWeightLb = 3360, Drive = Drivetrain.Awd, DragCoefficient = 0.35, FrontalAreaM2 = Area(1823, 1472),
            GearRatios = [3.231, 1.952, 1.321, 1.029, 1.129, 0.943],
            FinalDrives = [4.063, 4.063, 4.063, 4.063, 2.955, 2.955],
            StockTire = "235/35R19",
            MatchTokens = ["FOCUS", "RS"],
            SpecNotes = "Ford Australia 2016 Focus RS specifications (gearing, dimensions; kerb weight less the 75 kg driver it includes). Drag coefficient estimated.",
        },
        new VehiclePreset
        {
            Key = "gti-mk7", Make = "Volkswagen", Model = "GTI (Mk7)", YearFrom = 2015, YearTo = 2021,
            CurbWeightLb = 3062, Drive = Drivetrain.Fwd, DragCoefficient = 0.32, FrontalAreaM2 = Area(1799, 1442),
            MatchTokens = ["GTI"],
            SpecNotes = "Curb weight from iSeeCars (2018 GTI S 6MT). Drag and frontal area estimated. Gearing not verified, so the gear isn't named.",
        },
        new VehiclePreset
        {
            Key = "golf-r-mk7", Make = "Volkswagen", Model = "Golf R (Mk7)", YearFrom = 2015, YearTo = 2019,
            CurbWeightLb = 3300, Drive = Drivetrain.Awd, DragCoefficient = 0.32, FrontalAreaM2 = Area(1799, 1436),
            MatchTokens = ["GOLF", "R"], ExcludeTokens = ["GTI"],
            SpecNotes = "Curb weight from iSeeCars (2018 Golf R 6MT). Drag and frontal area estimated. Gearing not verified, so the gear isn't named.",
        },
    ];
}
