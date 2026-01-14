using VoxTrade.Models;

namespace VoxTrade.Services.Interface
{
    public interface IRolesRepository
    {
        Task<List<Roles>> GetAllRoles();
    }
}
