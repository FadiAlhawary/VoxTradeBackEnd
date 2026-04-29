using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using VoxTrade.Api.Data;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class MarketRepository : IMarketRepository
    {
        private readonly ILogger<MarketRepository> _logger;
        private readonly TradingDbContext _context;

        public MarketRepository(ILogger<MarketRepository> logger, TradingDbContext TradingDbContext)
        {
            _logger = logger;
            _context = TradingDbContext;
        }

        public async Task<PlaceOrderResponseDto> PlaceOrder(PlaceOrderRequestDto request)
        {
            const string sql = """
            SELECT public.place_demo_order(
                @UserId,
                @InstrumentId,
                @Side,
                @OrderType,
                @Quantity,
                @LimitPrice,
                @CurrencyId,
                @SourceCode
            )::text;
        """;

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var json = await connection.ExecuteScalarAsync<string>(sql, new
            {
                request.UserId,
                request.InstrumentId,
                request.Side,
                request.OrderType,
                request.Quantity,
                request.LimitPrice,
                request.CurrencyId,
                request.SourceCode
            });

            var result = JsonSerializer.Deserialize<PlaceOrderResponseDto>(
                json!,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            ); 


            return result;
        }
    }
}
