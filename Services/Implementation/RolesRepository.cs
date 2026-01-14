using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.DB;
using VoxTrade.Models;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class RolesRepository : IRolesRepository
    {
        private readonly ILogger<RolesRepository> _logger;
        private readonly TradingDbContext _context;

        public RolesRepository(ILogger<RolesRepository> logger, TradingDbContext TradingDbContext)
        {
             _logger = logger;
            _context = TradingDbContext;
        }
        public async Task<List<Roles>> GetAllRoles()
        {
            try
            {
                var result = await _context.Roles.FromSqlRaw($"SELECT * FROM {FunctionReferences.roles_getall}()").ToListAsync();
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
