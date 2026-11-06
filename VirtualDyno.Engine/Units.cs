namespace VirtualDyno.Engine;

/// <summary>Physical constants and unit conversions used across the engine.</summary>
public static class Units
{
    public const double Gravity = 9.80665;               // m/s²
    public const double KgPerLb = 0.45359237;
    public const double MetersPerSecondPerMph = 0.44704;
    public const double WattsPerHp = 745.69987;          // mechanical horsepower
    public const double HpTorqueConstant = 5252.113;     // hp = lb-ft × rpm / 5252
    public const double PaPerInHg = 3386.389;
    public const double PsiPerInHg = 0.491154;
    public const double PsiPerKpa = 0.145038;
    public const double SeaLevelBaroInHg = 29.921;
    public const double InchesPerMile = 63360.0;
    public const double GramsPerPound = 453.59237;

    public static double LbToKg(double lb) => lb * KgPerLb;
    public static double MphToMs(double mph) => mph * MetersPerSecondPerMph;
    public static double WattsToHp(double w) => w / WattsPerHp;
    public static double CToF(double c) => c * 1.8 + 32.0;
    public static double FToC(double f) => (f - 32.0) / 1.8;
    public static double TorqueFromHp(double hp, double rpm) => rpm > 0 ? hp * HpTorqueConstant / rpm : 0;
}
