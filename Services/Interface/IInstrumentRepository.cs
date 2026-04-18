using VoxTrade.Models.DTO;

namespace VoxTrade.Services.Interface
{
    public interface IInstrumentRepository
    {
        Task<List<InstrumentDto>> GetAllInstruments(bool activeOnly = true);
        Task<List<LookupItemDto>> GetInstrumentTypes();
        Task<List<LookupItemDto>> GetInstrumentStatuses();
    }
}
