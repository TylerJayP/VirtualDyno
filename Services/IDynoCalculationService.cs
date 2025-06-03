using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public interface IDynoCalculationService
    {
        /// <summary>
        /// Calculate horsepower using advanced algorithms with fallback to basic calculation
        /// </summary>
        double CalculateAdvancedHorsepower(AdvancedDynoData data, int gear, CarPreset carPreset,
            AdvancedDynoSettings? settings = null);

        double CalculateTorque(double horsepower, int rpm);
        List<DynoDataPoint> SmoothData(List<DynoDataPoint> data, int smoothingLevel);
        PeakValues CalculatePeakValues(List<DynoDataPoint> data, int carWeight);
    }
}