using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public interface IDynoCalculationService
    {
        /// <summary>
        /// Calculate horsepower using advanced algorithms with fallback to basic calculation
        /// </summary>
        double CalculateAdvancedHorsepower(AdvancedDynoData data, int gear, CarPreset carPreset, AdvancedDynoSettings? settings = null);
        double CalculateFromMAF(AdvancedDynoData data, CarPreset carPreset);
        double CalculateFromMAP(AdvancedDynoData data, CarPreset carPreset);
        double CalculateFromLoad(AdvancedDynoData data, CarPreset carPreset);
        double CalculateSimpleAFRCorrection(double afr, bool isForceInduction);
        double CalculateSimpleAtmosphericCorrection(double intakeTemp);
        double CalculateSimpleVE(int rpm);
        double GetCalibrationFactor(string carKey, string calculationMethod);
        double GetMAFCalibrationFactor(string carKey);
        double GetMAPCalibrationFactor(string carKey);
        double GetLoadCalibrationFactor(string carKey);
        double CalculateTorque(double horsepower, int rpm);
        List<DynoDataPoint> SmoothDataPreservePeaks(List<DynoDataPoint> data, int smoothingLevel);
        List<int> IdentifyGenuinePeaks(List<double> values);
        double SmoothSingleValue(List<DynoDataPoint> data, int index, Func<DynoDataPoint, double> selector, int smoothingLevel);
        PeakValues CalculatePeakValues(List<DynoDataPoint> data, int carWeight);


    }
}