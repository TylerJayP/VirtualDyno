using VirtualDyno.API.Models;

namespace VirtualDyno.API.Services
{
    public interface ICsvProcessingService
    {
        Task<DynoRun> ProcessCsvFileAsync(
            Stream csvStream,
            string fileName,
            CarConfiguration carConfig,
            CarPreset carPreset,
            int gear,
            string userId);
    }
}