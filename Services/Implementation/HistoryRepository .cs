using System;
using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class HistoryRepository : IHistoryRepository
    {
        private readonly TradingDbContext _context;
        private readonly ILogger<HistoryRepository> _logger;

        public HistoryRepository(TradingDbContext context, ILogger<HistoryRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<OrderHistoryDto>> GetOrderHistory(int userId, bool activeOnly = false)
        {
            try
            {
                const string sql = """
                    SELECT
                        o.id,
                        o.user_id AS UserId,
                        o.instrument_id AS InstrumentId,
                        COALESCE(i.symbol, '') AS Symbol,
                        COALESCE(i.name, '') AS InstrumentName,
                        o.order_type_id AS OrderTypeId,
                        COALESCE(ot.code, '') AS OrderType,
                        o.action_type_id AS ActionTypeId,
                        COALESCE(at.code, '') AS ActionType,
                        o.status_id AS StatusId,
                        COALESCE(st.code, '') AS Status,
                        o.quantity,
                        o.filled_quantity AS FilledQuantity,
                        o.remaining_quantity AS RemainingQuantity,
                        o.price,
                        o.limit_price AS LimitPrice,
                        o.stop_price AS StopPrice,
                        o.execution_price AS ExecutionPrice,
                        o.average_fill_price AS AverageFillPrice,
                        o.created_at AS CreatedAt,
                        o.updated_at AS UpdatedAt,
                        o.cancelled_at AS CancelledAt,
                        CASE
                            WHEN st.code = 'pending'
                                 AND COALESCE(o.filled_quantity, 0) = 0
                                 AND COALESCE(o.execution_price, 0) = 0
                            THEN true
                            ELSE false
                        END AS CanCancel
                    FROM public.orders o
                    LEFT JOIN public.instruments i ON i.id = o.instrument_id
                    LEFT JOIN public.lookup ot ON ot.id = o.order_type_id
                    LEFT JOIN public.lookup at ON at.id = o.action_type_id
                    LEFT JOIN public.lookup st ON st.id = o.status_id
                    WHERE o.user_id = @UserId
                      AND COALESCE(i.is_deleted, false) = false
                      AND (
                            @ActiveOnly = false
                            OR st.code IN ('pending', 'partially_filled')
                          )
                    ORDER BY o.created_at DESC;
                    """;

                var connection = _context.Database.GetDbConnection();

                if (connection.State != System.Data.ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<OrderHistoryDto>(
                    sql,
                    new { UserId = userId, ActiveOnly = activeOnly });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order history for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<TradeHistoryDto>> GetTradeHistory(int userId)
        {
            try
            {
                const string sql = """
                    SELECT
                        t.id,
                        t.order_id AS OrderId,
                        t.user_id AS UserId,
                        t.instrument_id AS InstrumentId,
                        COALESCE(i.symbol, '') AS Symbol,
                        COALESCE(i.name, '') AS InstrumentName,
                        COALESCE(t.side, '') AS Side,
                        t.price,
                        t.quantity,
                        t.trade_value AS TradeValue,
                        t.executed_at AS ExecutedAt
                    FROM public.trades t
                    LEFT JOIN public.instruments i ON i.id = t.instrument_id
                    WHERE t.user_id = @UserId
                      AND COALESCE(i.is_deleted, false) = false
                    ORDER BY t.executed_at DESC;
                    """;

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<TradeHistoryDto>(
                    sql,
                    new { UserId = userId });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get trade history for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<WalletHistoryDto>> GetWalletHistory(int userId)
        {
            try
            {
                const string sql = """
                    SELECT
                        wh.id,
                        wh.wallet_id AS WalletId,
                        wh.user_id AS UserId,
                        wh.order_id AS OrderId,
                        wh.trade_id AS TradeId,
                        wh.transaction_type AS TransactionType,
                        wh.amount,
                        wh.balance_before AS BalanceBefore,
                        wh.balance_after AS BalanceAfter,
                        wh.description,
                        wh.created_at AS CreatedAt
                    FROM public.wallet_history wh
                    WHERE wh.user_id = @UserId
                    ORDER BY wh.created_at DESC;
                    """;

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<WalletHistoryDto>(
                    sql,
                    new { UserId = userId });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get wallet history for user {UserId}", userId);
                throw;
            }
        }
        public async Task<List<WalletHistoryDto>> GetWalletHistoryWithDate(
    int userId,
    DateTime? from = null,
    DateTime? to = null)
        {
            try
            {
                const string sql = """
            SELECT
                wh.id,
                wh.wallet_id AS WalletId,
                wh.user_id AS UserId,
                wh.order_id AS OrderId,
                wh.trade_id AS TradeId,
                wh.transaction_type AS TransactionType,
                wh.amount,
                wh.balance_before AS BalanceBefore,
                wh.balance_after AS BalanceAfter,
                wh.description,
                wh.created_at AS CreatedAt
            FROM public.wallet_history wh
            WHERE wh.user_id = @UserId
              AND (@From IS NULL OR wh.created_at >= @From)
              AND (@To IS NULL OR wh.created_at <= @To)
            ORDER BY wh.created_at DESC;
            """;

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<WalletHistoryDto>(
                    sql,
                    new
                    {
                        UserId = userId,
                        From = from,
                        To = to
                    });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get wallet history for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> CancelPendingOrder(int userId, int orderId)
        {
            try
            {
                const string sql = """
                    UPDATE public.orders o
                    SET
                        status_id = (
                            SELECT l.id
                            FROM public.lookup l
                            INNER JOIN public.lookup_group lg
                                ON lg.id = l.lookup_group_id
                            WHERE lg.code = 'order_status'
                              AND l.code = 'cancelled'
                            LIMIT 1
                        ),
                        cancelled_at = CURRENT_TIMESTAMP,
                        updated_at = CURRENT_TIMESTAMP
                    WHERE o.id = @OrderId
                      AND o.user_id = @UserId
                      AND o.status_id = (
                            SELECT l.id
                            FROM public.lookup l
                            INNER JOIN public.lookup_group lg
                                ON lg.id = l.lookup_group_id
                            WHERE lg.code = 'order_status'
                              AND l.code = 'pending'
                            LIMIT 1
                      )
                      AND COALESCE(o.filled_quantity, 0) = 0
                      AND (o.execution_price IS NULL OR o.execution_price = 0);
                    """;

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var affected = await connection.ExecuteAsync(
                    sql,
                    new { UserId = userId, OrderId = orderId });

                return affected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cancel order {OrderId} for user {UserId}", orderId, userId);
                throw;
            }
        }

        public async Task<List<LookupItemDto>> GetOrderStatuses()
        {
            return await GetLookupByGroupCode("order_status");
        }

        public async Task<List<LookupItemDto>> GetOrderTypes()
        {
            return await GetLookupByGroupCode("order_type");
        }

        public async Task<List<LookupItemDto>> GetOrderActions()
        {
            return await GetLookupByGroupCode("order_action");
        }

        private async Task<List<LookupItemDto>> GetLookupByGroupCode(string groupCode)
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
                    WHERE lg.code = @GroupCode
                      AND COALESCE(l.is_deleted, false) = false
                    ORDER BY l.name;
                    """;

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var result = await connection.QueryAsync<LookupItemDto>(
                    sql,
                    new { GroupCode = groupCode });

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get lookup values for group {GroupCode}", groupCode);
                throw;
            }
        }
    }
}
