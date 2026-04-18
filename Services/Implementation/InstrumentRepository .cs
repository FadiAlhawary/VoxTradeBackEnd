using System;
using Dapper;
using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VoxTrade.Api.Data;
using VoxTrade.Data;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class InstrumentRepository : IInstrumentRepository
    {
        private readonly TradingDbContext _context;
        private readonly ILogger<InstrumentRepository> _logger;


        public InstrumentRepository(TradingDbContext context , ILogger<InstrumentRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<InstrumentDto>> GetAllInstruments(bool activeOnly = true)
        {
            try
            {
                const string sql = """
            SELECT
                i.id,
                i.symbol,
                i.name,
                i.tick_size AS TickSize,
                i.min_quantity AS MinQuantity,
                i.instrument_type AS InstrumentTypeId,
                lt.code AS InstrumentType,
                i.status AS StatusId,
                ls.code AS Status
            FROM public.instruments i
            LEFT JOIN public.lookup lt
                ON lt.id = i.instrument_type
            LEFT JOIN public.lookup ls
                ON ls.id = i.status
            WHERE COALESCE(i.is_deleted, false) = false
              AND (@ActiveOnly = false OR ls.code = 'active')
            ORDER BY i.symbol;
            """;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<InstrumentDto>(sql, new
                {
                    ActiveOnly = activeOnly
                });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get instruments");
                throw;
            }
        }

        public async Task<List<LookupItemDto>> GetInstrumentTypes()
        {
            try
            {
                const string sql = """
                    SELECT
                        l.id,
                        l.name,
                        l.code
                    FROM public.lookup l
                    INNER JOIN public.lookup_group lg
                        ON lg.id = l.lookup_group_id
                    WHERE lg.code = 'instrument_type'
                      AND COALESCE(l.is_deleted, false) = false
                    ORDER BY l.name;
                    """;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();
                var result = await connection.QueryAsync<LookupItemDto>(sql);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get instrument types");
                throw;
            }
        }

        public async Task<List<LookupItemDto>> GetInstrumentStatuses()
        {
            try
            {
                const string sql = """
                    SELECT
                        l.id,
                        l.name,
                        l.code
                    FROM public.lookup l
                    INNER JOIN public.lookup_group lg
                        ON lg.id = l.lookup_group_id
                    WHERE lg.code = 'instrument_status'
                      AND COALESCE(l.is_deleted, false) = false
                    ORDER BY l.name;
                    """;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync(); var result = await connection.QueryAsync<LookupItemDto>(sql);
                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get instrument statuses");
                throw;
            }
        }
    }
}
