namespace VirtualDyno.Engine;

/// <summary>How measured power is normalized to standard weather.</summary>
public enum CorrectionMode
{
    /// <summary>Power as measured in the conditions of the log.</summary>
    Uncorrected,
    /// <summary>SAE J1349 (25 °C, 99 kPa dry air). The modern standard most dyno sheets use.</summary>
    Sae,
    /// <summary>SAE J607 / "STD" (60 °F, 29.92 inHg). Older standard; reads about 4% higher than SAE.</summary>
    Std,
}

/// <summary>Air density and dyno correction factors.</summary>
public static class Atmosphere
{
    /// <summary>Standard-atmosphere barometric pressure at an altitude.</summary>
    public static double BaroFromAltitudeInHg(double altitudeFt) =>
        Units.SeaLevelBaroInHg * Math.Pow(1 - 6.8754e-6 * altitudeFt, 5.2559);

    /// <summary>Partial pressure of water vapor (Pa), Magnus formula.</summary>
    public static double VaporPressurePa(double tempF, double relativeHumidityPct)
    {
        double tc = Units.FToC(tempF);
        double saturation = 610.94 * Math.Exp(17.625 * tc / (tc + 243.04));
        return Math.Clamp(relativeHumidityPct, 0, 100) / 100.0 * saturation;
    }

    /// <summary>Moist air density in kg/m³.</summary>
    public static double AirDensity(double baroInHg, double tempF, double relativeHumidityPct)
    {
        double tk = Units.FToC(tempF) + 273.15;
        double total = baroInHg * Units.PaPerInHg;
        double vapor = VaporPressurePa(tempF, relativeHumidityPct);
        double dry = total - vapor;
        return dry / (287.058 * tk) + vapor / (461.495 * tk);
    }

    /// <summary>Correction factor that converts measured power to the chosen standard conditions.</summary>
    public static double CorrectionFactor(CorrectionMode mode, double baroInHg, double tempF, double relativeHumidityPct) =>
        mode switch
        {
            CorrectionMode.Sae => SaeJ1349(baroInHg, tempF, relativeHumidityPct),
            CorrectionMode.Std => SaeJ607(baroInHg, tempF),
            _ => 1.0,
        };

    /// <summary>SAE J1349: CF = 1.18 × (990 / Pd) × √((T + 273) / 298) − 0.18, Pd in mbar dry, T in °C.</summary>
    public static double SaeJ1349(double baroInHg, double tempF, double relativeHumidityPct)
    {
        double dryMbar = (baroInHg * Units.PaPerInHg - VaporPressurePa(tempF, relativeHumidityPct)) / 100.0;
        double tc = Units.FToC(tempF);
        return 1.18 * (990.0 / dryMbar) * Math.Sqrt((tc + 273.0) / 298.0) - 0.18;
    }

    /// <summary>SAE J607 ("STD"): CF = (29.92 / P) × √((T + 460) / 520), P in inHg, T in °F.</summary>
    public static double SaeJ607(double baroInHg, double tempF) =>
        29.92 / baroInHg * Math.Sqrt((tempF + 460.0) / 520.0);
}
