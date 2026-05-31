using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using VoxTrade.Api.Data;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;
using VoxTrade.Services;

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
            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var frozenMessage = await WalletFreezeGuard.GetFrozenMessageIfAnyAsync(
                connection,
                request.UserId);
            if (frozenMessage != null)
            {
                return new PlaceOrderResponseDto
                {
                    Success = false,
                    Message = frozenMessage
                };
            }

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

            if (string.IsNullOrWhiteSpace(json))
            {
                return new PlaceOrderResponseDto
                {
                    Success = false,
                    Message = "Empty response from order service"
                };
            }

            var result = JsonSerializer.Deserialize<PlaceOrderResponseDto>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }
            );

            return result ?? new PlaceOrderResponseDto
            {
                Success = false,
                Message = "Invalid response from order service"
            };
        }
    }
}
