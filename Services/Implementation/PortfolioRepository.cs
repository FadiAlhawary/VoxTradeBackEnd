using System.Data;
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
    public class PortfolioRepository : IPortfolioRepository
    {
        private readonly TradingDbContext _context;
        private readonly ILogger<PortfolioRepository> _logger;


        public PortfolioRepository(TradingDbContext context, ILogger<PortfolioRepository> logger)
        {
            _context = context;
            _logger = logger;
        }
        public async Task<List<PortfolioPositionDto>> GetPortfolio(int userId)
        {
            const string sql = """
SELECT
    p.id AS PositionId,
    p.user_id AS UserId,
    p.instrument_id AS InstrumentId,
    i.symbol AS Symbol,
    i.name AS InstrumentName,
    i.short_name,
    p.quantity AS Quantity,
    p.reserved_quantity AS ReservedQuantity,
    (p.quantity - p.reserved_quantity) AS AvailableQuantity,
    p.average_cost AS AverageCost,
    p.realized_pnl AS RealizedPnl,
    mq.bid_price AS BidPrice,
    mq.ask_price AS AskPrice,
    mq.last_trade_price AS LastTradePrice,
    COALESCE(mq.bid_price, mq.last_trade_price, p.average_cost) AS CurrentPrice,
    p.quantity * COALESCE(mq.bid_price, mq.last_trade_price, p.average_cost) AS MarketValue,
    (COALESCE(mq.bid_price, mq.last_trade_price, p.average_cost) - p.average_cost) * p.quantity AS UnrealizedPnl,
    p.updated_at AS UpdatedAt
FROM public.positions p
JOIN public.instruments i ON i.id = p.instrument_id
LEFT JOIN LATERAL (
    SELECT bid_price, ask_price, last_trade_price
    FROM public.market_quotes
    WHERE instrument_id = p.instrument_id
    ORDER BY quote_timestamp DESC
    LIMIT 1
) mq ON true
WHERE p.user_id = @UserId
  AND p.quantity > 0
ORDER BY i.symbol;
""";

            var connection = _context.Database.GetDbConnection();

            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var positions = await connection.QueryAsync<PortfolioPositionDto>(
                sql,
                new { UserId = userId }
            );

            return positions.ToList();
        }

        public async Task<HoldingSummaryDto> GetHoldingSummary(int userId, string symbol)
        {
            const string sql = """
SELECT
    i.symbol AS Symbol,
    i.id AS InstrumentId,
    COALESCE(p.quantity, 0) AS QuantityOwned,
    COALESCE(p.quantity * COALESCE(mq.last_trade_price, mq.bid_price, p.average_cost, 0), 0) AS ValueUsd
FROM public.instruments i
LEFT JOIN public.positions p
    ON p.instrument_id = i.id
   AND p.user_id = @UserId
   AND p.quantity > 0
LEFT JOIN LATERAL (
    SELECT bid_price, ask_price, last_trade_price
    FROM public.market_quotes
    WHERE instrument_id = i.id
    ORDER BY quote_timestamp DESC
    LIMIT 1
) mq ON true
WHERE COALESCE(i.is_deleted, false) = false
  AND (
        UPPER(i.symbol) = UPPER(@Symbol)
        OR UPPER(i.short_name) = UPPER(@Symbol)
      )
ORDER BY
    CASE WHEN UPPER(i.symbol) = UPPER(@Symbol) THEN 0 ELSE 1 END,
    i.id
LIMIT 1;
""";

            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var result = await connection.QueryFirstOrDefaultAsync<HoldingSummaryDto>(
                sql,
                new { UserId = userId, Symbol = symbol }
            );

            return result ?? new HoldingSummaryDto
            {
                Symbol = symbol,
                InstrumentId = null,
                QuantityOwned = 0,
                ValueUsd = 0
            };
        }

        public async Task<List<PortfolioProfitLossPointDto>> GetProfitLossChart(
    int userId,
    DateTime? from = null,
    DateTime? to = null)
        {
            const string sql = """
        SELECT
            snapshot_at AS Time,
            total_value AS TotalValue,
            cash_balance AS CashBalance,
            positions_value AS PositionsValue,
            realized_pnl AS RealizedPnl,
            unrealized_pnl AS UnrealizedPnl,
            total_value - FIRST_VALUE(total_value) OVER (ORDER BY snapshot_at) AS ProfitLoss
        FROM public.portfolio_snapshots
        WHERE user_id = @UserId
          AND (@From IS NULL OR snapshot_at >= @From)
          AND (@To IS NULL OR snapshot_at <= @To)
        ORDER BY snapshot_at;
    """;

            var connection = _context.Database.GetDbConnection();

            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();

            var result = await connection.QueryAsync<PortfolioProfitLossPointDto>(
                sql,
                new { UserId = userId, From = from, To = to });

            return result.ToList();
        }
    }
}
