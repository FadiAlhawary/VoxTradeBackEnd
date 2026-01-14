using VoxTrade.Models;

namespace VoxTrade.Services.Interface
{
    public interface IUIThemesRepository
    {
        Task<List<UIThemes>> GetUIThemeByPurposeId(int PurposeId);
    }
}
