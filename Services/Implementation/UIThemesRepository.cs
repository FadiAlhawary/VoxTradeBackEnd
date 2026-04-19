using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class UIThemesRepository : IUIThemesRepository
    {

        private readonly ILogger<UIThemesRepository> _logger;
        private readonly TradingDbContext _context;

        public UIThemesRepository(ILogger<UIThemesRepository> logger, TradingDbContext TradingDbContext)
        {
            _logger = logger;
            _context = TradingDbContext;
        }
        public async Task<List<UIThemes>> GetUIThemeByPurposeId(int PurposeId)
        {
            try
            {
                var result = await _context.UIThemes.Where(t => t.id == PurposeId).ToListAsync();
                return result;

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get roles");
                throw;
            }
        }
    }
}
