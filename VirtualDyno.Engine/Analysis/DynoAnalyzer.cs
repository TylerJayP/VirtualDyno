using VirtualDyno.Engine.Logs;
using VirtualDyno.Engine.Numerics;
using VirtualDyno.Engine.Vehicles;

namespace VirtualDyno.Engine.Analysis;

/// <summary>
/// Road dyno: turns a full-throttle pull into wheel horsepower and torque.
/// <para>
/// An inertia chassis dyno (a Dynojet, for example) measures how quickly the car spins up a drum of
/// known inertia. On the road the car itself is the drum: its known mass replaces the drum's known
/// inertia. At every sample, the force at the tires is what it took to accelerate the car plus what it
/// took to push through the air, roll the tires and climb any grade:
/// </para>
/// <code>F = m_eff·a + ½·ρ·CdA·v² + Crr·m·g + m·g·grade      P = F·v</code>
/// <para>
/// Speed comes from engine RPM (fine resolution) divided by the RPM-per-mph ratio measured from the
/// log's own vehicle speed, so gear ratio and tire size never have to be typed in. Acceleration is the
/// derivative of a smoothing spline whose smoothness is estimated from the data (REML) through RPM over time.
/// </para>
/// </summary>
public static class DynoAnalyzer
{
    private const double MinSpeedForRatioMph = 15;
    private const double DefaultWeightAllowanceLb = 200;   // driver + some fuel when the user hasn't said
    private const double GenericDragAreaM2 = 0.70;
    private const double GenericWeightLb = 3300;
    private const double DefaultAmbientF = 77;
    private const double SlipMarginSec = 0.3;

    public static DynoResult Analyze(ParsedLog log, DynoSettings? settings = null)
    {
        settings ??= new DynoSettings();
        if (!log.Has(Channel.Time))
            throw new DynoAnalysisException("This log has no time column, so acceleration can't be measured. Log with a timestamp.");
        if (!log.Has(Channel.Rpm))
            throw new DynoAnalysisException("This log has no RPM column.");

        var warnings = new List<DynoWarning>();
        var time = log[Channel.Time];
        var rpmRaw = log[Channel.Rpm];

        // ---- vehicle ------------------------------------------------------------------------
        VehiclePreset? preset = VehicleCatalog.Find(settings.VehicleKey);
        bool autoDetected = false;
        if (preset is null && settings.VehicleKey is null)
        {
            preset = VehicleCatalog.MatchLogVehicle(log.Source.Vehicle);
            autoDetected = preset is not null;
        }

        // ---- pull ---------------------------------------------------------------------------
        var pulls = PullDetector.Find(log);
        if (pulls.Count == 0)
        {
            throw new DynoAnalysisException(
                "No full-throttle pull found. The dyno needs at least 1.5 seconds flat out in one gear with RPM climbing 1,000+ RPM.");
        }
        var pull = settings.PullIndex is int pi && pi >= 0 && pi < pulls.Count ? pulls[pi] : PullDetector.Best(pulls)!;
        if (pulls.Count > 1)
        {
            warnings.Add(new("PULL_MULTIPLE", WarningSeverity.Info,
                $"Found {pulls.Count} pulls in this log. Showing pull {pull.Index + 1} ({pull.StartRpm:0}–{pull.EndRpm:0} RPM)."));
        }

        var idx = new List<int>();
        double lastT = double.NegativeInfinity;
        for (int i = pull.Start; i <= pull.End; i++)
        {
            if (double.IsNaN(time[i]) || double.IsNaN(rpmRaw[i]) || time[i] <= lastT + 1e-6) continue;
            idx.Add(i);
            lastT = time[i];
        }
        if (idx.Count < 8) throw new DynoAnalysisException("The pull has too few samples to measure acceleration.");

        int n = idx.Count;
        double t0 = time[idx[0]];
        double[] t = idx.Select(i => time[i] - t0).ToArray();
        double[] rpm = idx.Select(i => rpmRaw[i]).ToArray();
        double duration = t[^1];
        double sampleRateHz = (n - 1) / duration;

        // ---- gearing ------------------------------------------------------------------------
        var slip = new bool[n];
        double rpmPerMph;
        ValueSource ratioSource;
        int? gear = null;
        double? overallRatio = null;
        string? tire = settings.TireSize ?? preset?.StockTire;

        var speed = log.TryGet(Channel.Speed);
        if (speed is not null && TryMeasureRatio(idx, rpm, speed, slip, out double measured))
        {
            rpmPerMph = measured * (1 + settings.SpeedometerErrorPct / 100.0);
            ratioSource = ValueSource.Measured;
            if (preset is not null)
            {
                var detected = VehicleCatalog.DetectGear(preset, rpmPerMph, settings.TireSize);
                if (detected is not null)
                {
                    gear = detected.Value.Gear;
                    overallRatio = detected.Value.OverallRatio;
                }
                else if (preset.HasGearing)
                {
                    warnings.Add(new("GEAR_UNKNOWN", WarningSeverity.Info,
                        $"The RPM-to-speed ratio ({rpmPerMph:0.0} RPM/mph) doesn't match any gear on a stock {preset.Model}. " +
                        "That's fine for the power numbers. If you run a different tire size or gearing, set the tire size."));
                }
            }
        }
        else
        {
            double? diameter = TireSize.DiameterInches(tire);
            overallRatio = settings.OverallRatio ?? (settings.Gear is int g ? preset?.OverallRatio(g) : null);
            if (overallRatio is null || diameter is null)
            {
                throw new DynoAnalysisException(speed is null
                    ? "This log has no vehicle speed. Pick your car and the gear you logged in (or enter the overall gear ratio and tire size)."
                    : "Vehicle speed in this log is too low or missing during the pull. Pick the gear you logged in instead.");
            }
            rpmPerMph = TireSize.RpmPerMph(overallRatio.Value, diameter.Value);
            ratioSource = settings.OverallRatio is not null ? ValueSource.User : ValueSource.Catalog;
            gear = settings.Gear;
            warnings.Add(new("NO_SPEED", WarningSeverity.Info,
                "Speed was calculated from gearing and tire size because the log has no usable vehicle speed. Wheelspin can't be detected."));
        }

        // Slip contaminates the acceleration estimate around it too (the curve is smoothed through it),
        // so points within a few tenths of a second of slip are treated as slip as well.
        if (slip.Any(s => s))
        {
            var core = (bool[])slip.Clone();
            for (int i = 0; i < n; i++)
            {
                if (!core[i]) continue;
                for (int j = 0; j < n; j++)
                {
                    if (Math.Abs(t[j] - t[i]) <= SlipMarginSec) slip[j] = true;
                }
            }
        }

        int slipCount = slip.Count(s => s);
        double? slipFrom = null, slipTo = null;
        if (slipCount > 0)
        {
            slipFrom = Enumerable.Range(0, n).Where(i => slip[i]).Min(i => rpm[i]);
            slipTo = Enumerable.Range(0, n).Where(i => slip[i]).Max(i => rpm[i]);
            warnings.Add(new("SLIP", WarningSeverity.Warning,
                $"Clutch slip or wheelspin between {slipFrom:0} and {slipTo:0} RPM: engine speed ran ahead of vehicle speed. " +
                "The curve is bridged through that stretch (shown dashed) and left off the peaks. A higher gear usually fixes it."));
        }

        if (gear is <= 2)
        {
            warnings.Add(new("GEAR_LOW", WarningSeverity.Warning,
                $"Pulled in {Ordinal(gear.Value)} gear. Low gears spin the engine up so fast that more power goes into its own rotating mass, " +
                "and wheelspin is likely. 3rd or 4th gives numbers closer to a chassis dyno."));
        }
        else if (gear is >= 5)
        {
            warnings.Add(new("GEAR_HIGH", WarningSeverity.Info,
                $"Pulled in {Ordinal(gear.Value)} gear. At high speed, aero drag and road grade are a bigger share of the result, so weight, drag and a flat road matter more."));
        }

        // ---- environment --------------------------------------------------------------------
        double baro;
        ValueSource baroSource;
        double? logBaro = MedianOver(log.TryGet(Channel.Baro), idx);
        if (settings.BaroInHg is double userBaro) { baro = userBaro; baroSource = ValueSource.User; }
        else if (logBaro is double lb && lb is > 15 and < 32) { baro = lb; baroSource = ValueSource.Log; }
        else if (settings.AltitudeFt is double alt) { baro = Atmosphere.BaroFromAltitudeInHg(alt); baroSource = ValueSource.User; }
        else
        {
            baro = Units.SeaLevelBaroInHg;
            baroSource = ValueSource.Default;
            warnings.Add(new("BARO_DEFAULT", WarningSeverity.Warning,
                "No altitude or barometric pressure, so sea level was assumed. Enter your altitude for correct air density and correction."));
        }

        double ambient;
        ValueSource ambientSource;
        double? logIat = MedianOver(log.TryGet(Channel.IntakeTemp), idx);
        if (settings.AmbientTempF is double userTemp) { ambient = userTemp; ambientSource = ValueSource.User; }
        else if (logIat is double iat && iat is > -40 and < 140) { ambient = iat; ambientSource = ValueSource.Log; }
        else
        {
            ambient = DefaultAmbientF;
            ambientSource = ValueSource.Default;
            warnings.Add(new("AMBIENT_DEFAULT", WarningSeverity.Warning,
                $"No intake air temperature in the log, so {DefaultAmbientF}°F was assumed. Enter the outside temperature."));
        }

        double humidity = Math.Clamp(settings.HumidityPct, 0, 100);
        double airDensity = Atmosphere.AirDensity(baro, ambient, humidity);
        double cf = Atmosphere.CorrectionFactor(settings.Correction, baro, ambient, humidity);

        // ---- vehicle parameters -------------------------------------------------------------
        double weight;
        ValueSource weightSource;
        if (settings.WeightLb is double w && w > 0) { weight = w; weightSource = ValueSource.User; }
        else if (preset is not null) { weight = preset.CurbWeightLb + DefaultWeightAllowanceLb; weightSource = ValueSource.Catalog; }
        else { weight = GenericWeightLb; weightSource = ValueSource.Default; }
        if (weightSource != ValueSource.User)
        {
            warnings.Add(new("WEIGHT_DEFAULT", WarningSeverity.Warning,
                $"Using {weight:#,0} lb ({(preset is null ? "a generic car" : "factory curb weight + 200 lb for driver and fuel")}). " +
                "Weight is the biggest lever on accuracy: every 1% off is about 1% off the result. A truck-stop scale gives the real number."));
        }

        double dragArea;
        ValueSource dragSource;
        if (settings.DragAreaM2 is double cda && cda > 0) { dragArea = cda; dragSource = ValueSource.User; }
        else if (preset is not null) { dragArea = preset.DragAreaM2; dragSource = ValueSource.Catalog; }
        else { dragArea = GenericDragAreaM2; dragSource = ValueSource.Default; }

        if (preset is null)
        {
            warnings.Add(new("NO_VEHICLE", WarningSeverity.Warning,
                "Car not identified, so generic weight and drag were used. Pick your car or enter weight and drag area."));
        }

        double[]? boost = BoostChannel(log, baro);
        double? peakBoost = MaxOver(boost, idx);
        bool turbo = preset?.Turbo ?? (peakBoost is > 3);
        var drive = preset?.Drive ?? Drivetrain.Fwd;

        // ---- fit and physics ----------------------------------------------------------------
        int bias = Math.Clamp(settings.SmoothingBias, -3, 3);
        // Fit through the clean samples only. Where the tires or clutch slipped, RPM says nothing about
        // how fast the car was accelerating, so the curve bridges that stretch from both sides.
        var clean = Enumerable.Range(0, n).Where(i => !slip[i]).ToList();
        if (clean.Count < Math.Max(8, n / 2)) clean = Enumerable.Range(0, n).ToList();
        var spline = PSpline.FitAuto(clean.Select(i => t[i]).ToArray(), clean.Select(i => rpm[i]).ToArray(), Math.Pow(2, bias));
        var smoothRpm = new double[n];
        var rpmRate = new double[n];   // RPM per second
        for (int i = 0; i < n; i++) (smoothRpm[i], rpmRate[i]) = spline.At(t[i]);

        double massKg = Units.LbToKg(weight);
        double effectiveMass = massKg * settings.RotationalMassFactor;
        double gradeAngle = Math.Atan(settings.RoadGradePct / 100.0);

        var whpRaw = new double[n];
        var wtqRaw = new double[n];
        var parts = new (double Accel, double Aero, double Roll, double Grade)[n];
        for (int i = 0; i < n; i++)
        {
            double v = Units.MphToMs(smoothRpm[i] / rpmPerMph);
            double a = Units.MphToMs(rpmRate[i] / rpmPerMph);
            double fAccel = effectiveMass * a;
            double fAero = 0.5 * airDensity * dragArea * v * v;
            double fRoll = settings.RollingResistance * massKg * Units.Gravity * Math.Cos(gradeAngle);
            double fGrade = massKg * Units.Gravity * Math.Sin(gradeAngle);
            parts[i] = (fAccel * v, fAero * v, fRoll * v, fGrade * v);
            whpRaw[i] = Units.WattsToHp((fAccel + fAero + fRoll + fGrade) * v);
            wtqRaw[i] = Units.TorqueFromHp(whpRaw[i], smoothRpm[i]);
        }

        // Peaks: skip slipping points and the very ends, where a spline has the least data on one side.
        double edge = Math.Min(0.15, 0.05 * duration);
        var usable = Enumerable.Range(0, n).Where(i => !slip[i] && t[i] >= edge && t[i] <= duration - edge).ToList();
        if (usable.Count == 0) usable = Enumerable.Range(0, n).ToList();
        int hpAt = usable.MaxBy(i => whpRaw[i]);
        int tqAt = usable.MaxBy(i => wtqRaw[i]);

        var peaks = new PeakValues(
            Whp: Math.Round(whpRaw[hpAt] * cf, 1),
            WhpRpm: Math.Round(smoothRpm[hpAt]),
            Wtq: Math.Round(wtqRaw[tqAt] * cf, 1),
            WtqRpm: Math.Round(smoothRpm[tqAt]),
            BoostPsi: peakBoost is null ? null : Math.Round(peakBoost.Value, 1),
            UncorrectedWhp: Math.Round(whpRaw[hpAt], 1),
            UncorrectedWtq: Math.Round(wtqRaw[tqAt], 1));

        var breakdown = new PowerBreakdown(
            Math.Round(Units.WattsToHp(parts[hpAt].Accel), 1),
            Math.Round(Units.WattsToHp(parts[hpAt].Aero), 1),
            Math.Round(Units.WattsToHp(parts[hpAt].Roll), 1),
            Math.Round(Units.WattsToHp(parts[hpAt].Grade), 1));

        var afr = log.TryGet(Channel.Afr);
        var spark = log.TryGet(Channel.SparkAdvance);
        var knock = log.TryGet(Channel.KnockRetard);
        var iatCh = log.TryGet(Channel.IntakeTemp);
        var charge = log.TryGet(Channel.ChargeTemp);

        var points = new List<DynoPoint>(n);
        for (int i = 0; i < n; i++)
        {
            int r = idx[i];
            points.Add(new DynoPoint(
                TimeSec: Math.Round(t[i], 3),
                Rpm: Math.Round(smoothRpm[i], 1),
                Whp: Math.Round(whpRaw[i] * cf, 2),
                Wtq: Math.Round(wtqRaw[i] * cf, 2),
                Mph: Math.Round(smoothRpm[i] / rpmPerMph, 2),
                BoostPsi: At(boost, r, 2),
                Afr: At(afr, r, 2),
                SparkAdvance: At(spark, r, 2),
                KnockRetard: At(knock, r, 2),
                IntakeTempF: At(iatCh, r, 1),
                ChargeTempF: At(charge, r, 1),
                Slip: slip[i]));
        }

        // ---- cross-check and data-quality warnings -----------------------------------------
        var crossCheck = MafCheck(log, idx, smoothRpm, whpRaw, slip, peaks.WhpRpm, drive, turbo);
        if (crossCheck is { Agrees: false })
        {
            string direction = crossCheck.ImpliedBsfc < crossCheck.ExpectedBsfcLow ? "more" : "less";
            warnings.Add(new("MAF_DISAGREES", WarningSeverity.Warning,
                $"The fuel burned (from MAF and AFR) supports about {crossCheck.EstimatedWhpLow:0}–{crossCheck.EstimatedWhpHigh:0} whp, " +
                $"but the pull measured {peaks.UncorrectedWhp:0} uncorrected — {direction} power than the fuel can explain. " +
                "Check the weight, look for a hill or tailwind, and make sure the clutch wasn't slipping."));
        }

        AddDataQualityWarnings(warnings, log, idx, rpm, t, turbo, boost, sampleRateHz, pull);

        if (settings.Correction != CorrectionMode.Uncorrected && turbo && cf > 1.07)
        {
            warnings.Add(new("CORRECTION_LARGE", WarningSeverity.Info,
                $"At this altitude the {(settings.Correction == CorrectionMode.Sae ? "SAE" : "STD")} correction adds {(cf - 1) * 100:0}%. " +
                "A turbo makes up much of the thin-air loss on its own, so corrected numbers will read optimistic. Compare uncorrected runs to each other."));
        }

        var vehicleUsed = new VehicleUsed(
            preset?.Key,
            preset?.Name ?? "Unknown car",
            autoDetected,
            weight,
            weightSource,
            Math.Round(dragArea, 3),
            dragSource,
            drive,
            turbo,
            settings.RollingResistance,
            settings.RotationalMassFactor,
            settings.RoadGradePct);

        var gearing = new GearingInfo(
            Math.Round(rpmPerMph, 2), ratioSource, gear,
            overallRatio is null ? null : Math.Round(overallRatio.Value, 3),
            tire, slipCount, slipFrom, slipTo);

        var environment = new EnvironmentInfo(
            Math.Round(baro, 2), baroSource, Math.Round(ambient, 1), ambientSource, humidity,
            Math.Round(airDensity, 4), settings.Correction, Math.Round(cf, 4));

        var logInfo = new LogInfo(log.Source.Device, log.Source.Firmware, log.Source.Vehicle, log.Source.Map, log.RowCount, log.Matches);

        return new DynoResult(
            logInfo, vehicleUsed, pulls, pull.Index, gearing, environment,
            new SmoothingInfo(bias, spline.Lambda, Math.Round(spline.EffectiveDegreesOfFreedom, 1), Math.Round(sampleRateHz, 1)),
            peaks, breakdown, crossCheck, points,
            warnings.OrderByDescending(x => x.Severity).ToList());
    }

    /// <summary>
    /// RPM per mph through the origin, weighted toward higher speeds where the integer speed reading is
    /// most precise. Points where RPM runs ahead of wheel speed for two or more samples are slip.
    /// </summary>
    internal static bool TryMeasureRatio(List<int> idx, double[] rpm, double[] speed, bool[] slip, out double ratio)
    {
        ratio = double.NaN;
        var usable = new List<int>();
        for (int i = 0; i < idx.Count; i++)
        {
            double s = speed[idx[i]];
            if (!double.IsNaN(s) && s >= MinSpeedForRatioMph) usable.Add(i);
        }
        if (usable.Count < 6) return false;

        static double Fit(IEnumerable<int> set, double[] r, double[] s, List<int> idx)
        {
            double num = 0, den = 0;
            foreach (int i in set)
            {
                num += r[i] * s[idx[i]];
                den += s[idx[i]] * s[idx[i]];
            }
            return num / den;
        }

        ratio = Fit(usable, rpm, speed, idx);
        for (int pass = 0; pass < 2; pass++)
        {
            Array.Clear(slip);
            var candidate = new bool[idx.Count];
            foreach (int i in usable)
            {
                double s = speed[idx[i]];
                double deviation = rpm[i] / s / ratio - 1;
                // Speed is often logged in whole mph, so allow for half a mph of rounding plus margin.
                double tolerance = 0.04 + 0.6 / s;
                candidate[i] = deviation > tolerance;
            }
            for (int i = 0; i < idx.Count; i++)
            {
                if (!candidate[i]) continue;
                bool neighbor = (i > 0 && candidate[i - 1]) || (i + 1 < idx.Count && candidate[i + 1]);
                if (neighbor) slip[i] = true;
            }
            var clean = usable.Where(i => !slip[i]).ToList();
            if (clean.Count < 6) break;
            ratio = Fit(clean, rpm, speed, idx);
        }
        return double.IsFinite(ratio) && ratio > 0;
    }

    private static MafCrossCheck? MafCheck(ParsedLog log, List<int> idx, double[] rpmFit, double[] whpRaw, bool[] slip,
        double peakRpm, Drivetrain drive, bool turbo)
    {
        var maf = log.TryGet(Channel.Maf);
        if (maf is null) return null;
        var afr = log.TryGet(Channel.Afr);

        var window = Enumerable.Range(0, idx.Count)
            .Where(i => !slip[i] && Math.Abs(rpmFit[i] - peakRpm) <= 300 && !double.IsNaN(maf[idx[i]]) && maf[idx[i]] > 5)
            .ToList();
        if (window.Count < 3) return null;

        double mafGs = window.Average(i => maf[idx[i]]);
        double whp = window.Average(i => whpRaw[i]);
        List<double> afrValues = afr is null ? [] : window.Select(i => afr[idx[i]]).Where(a => a is > 9 and < 17).ToList();
        bool assumed = afrValues.Count == 0;
        double afrValue = assumed ? (turbo ? 11.5 : 12.8) : afrValues.Average();

        double fuelLbHr = mafGs / afrValue * 3600.0 / Units.GramsPerPound;
        double loss = drive switch { Drivetrain.Awd => 0.20, Drivetrain.Rwd => 0.15, _ => 0.12 };
        double crankHp = whp / (1 - loss);
        double implied = fuelLbHr / crankHp;
        (double low, double high) = turbo ? (0.50, 0.68) : (0.42, 0.55);

        return new MafCrossCheck(
            Math.Round(mafGs, 1), Math.Round(afrValue, 2), assumed, Math.Round(fuelLbHr, 1), Math.Round(implied, 3),
            low, high,
            Math.Round(fuelLbHr / high * (1 - loss), 0), Math.Round(fuelLbHr / low * (1 - loss), 0),
            implied >= low && implied <= high);
    }

    private static void AddDataQualityWarnings(List<DynoWarning> warnings, ParsedLog log, List<int> idx, double[] rpm, double[] t,
        bool turbo, double[]? boost, double sampleRateHz, Pull pull)
    {
        if (sampleRateHz < 10)
        {
            warnings.Add(new("LOW_RATE", WarningSeverity.Warning,
                $"Logged at {sampleRateHz:0.0} samples/second. 15+ gives much steadier curves; log fewer channels to speed it up."));
        }

        if (pull.RpmSpan < 2000)
        {
            warnings.Add(new("SHORT_PULL", WarningSeverity.Info,
                $"Short pull ({pull.StartRpm:0}–{pull.EndRpm:0} RPM). Start lower and run to redline for the full curve."));
        }

        var knock = log.TryGet(Channel.KnockRetard);
        if (knock is not null)
        {
            int worst = -1;
            for (int i = 0; i < idx.Count; i++)
            {
                double k = knock[idx[i]];
                if (!double.IsNaN(k) && (worst < 0 || k > knock[idx[worst]])) worst = i;
            }
            if (worst >= 0 && knock[idx[worst]] >= 0.5)
            {
                double deg = knock[idx[worst]];
                warnings.Add(new("KNOCK", deg >= 2 ? WarningSeverity.Warning : WarningSeverity.Info,
                    $"Knock retard up to {deg:0.0}° around {rpm[worst]:0} RPM."));
            }
        }

        var afr = log.TryGet(Channel.Afr);
        if (turbo && afr is not null && boost is not null)
        {
            int leanest = -1;
            for (int i = 0; i < idx.Count; i++)
            {
                double a = afr[idx[i]], b = boost[idx[i]];
                if (double.IsNaN(a) || double.IsNaN(b) || b < 5 || a > 18) continue;
                if (leanest < 0 || a > afr[idx[leanest]]) leanest = i;
            }
            if (leanest >= 0 && afr[idx[leanest]] > 12.5)
            {
                warnings.Add(new("LEAN", WarningSeverity.Warning,
                    $"AFR reached {afr[idx[leanest]]:0.0} under boost around {rpm[leanest]:0} RPM. Most turbo tunes target 11–12 at full throttle."));
            }
        }
    }

    private static double[]? BoostChannel(ParsedLog log, double baroInHg)
    {
        if (log.TryGet(Channel.Boost) is { } gauge) return gauge;
        if (log.TryGet(Channel.ManifoldAbsolute) is { } abs)
        {
            double baroPsi = baroInHg * Units.PsiPerInHg;
            return abs.Select(v => double.IsNaN(v) ? v : v - baroPsi).ToArray();
        }
        return null;
    }

    private static double? MedianOver(double[]? values, List<int> idx)
    {
        if (values is null) return null;
        double m = LogParser.Median(idx.Select(i => values[i]));
        return double.IsNaN(m) ? null : m;
    }

    private static double? MaxOver(double[]? values, List<int> idx)
    {
        if (values is null) return null;
        var finite = idx.Select(i => values[i]).Where(v => !double.IsNaN(v)).ToList();
        return finite.Count == 0 ? null : finite.Max();
    }

    private static double? At(double[]? values, int row, int digits) =>
        values is null || double.IsNaN(values[row]) ? null : Math.Round(values[row], digits);

    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
}
