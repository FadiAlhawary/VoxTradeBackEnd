using Dapper;
using Microsoft.AspNetCore.SignalR;
using Npgsql;

namespace VoxTrade.MarketHubs
{
    public class OrderMatchingService
    {
        private readonly string _connectionString;
        private readonly ILogger<OrderMatchingService> _logger;
        private readonly IHubContext<MarketHub> _hubContext;

        public OrderMatchingService(IConfiguration configuration, ILogger<OrderMatchingService> logger, IHubContext<MarketHub> hubContext)
        {
            _connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Postgres connection string missing.");
            _logger = logger;
            _hubContext = hubContext;
        }

        public async Task MatchOrdersForSymbolAsync(string symbol, decimal currentPrice, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(symbol) || currentPrice <= 0)
                return;

            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(ct);

                const string findSql = """
                    SELECT
                        o.id              AS Id,
                        o.user_id         AS UserId,
                        o.instrument_id   AS InstrumentId,
                        o.quantity        AS Quantity,
                        o.price           AS LimitPrice,
                        LOWER(at.code)    AS ActionType
                    FROM public.orders o
                    INNER JOIN public.instruments i ON i.id = o.instrument_id
                    LEFT JOIN public.lookup st ON st.id = o.status_id
                    LEFT JOIN public.lookup at ON at.id = o.action_type_id
                    LEFT JOIN public.lookup ot ON ot.id = o.order_type_id
                    WHERE UPPER(i.symbol) = UPPER(@Symbol)
                      AND LOWER(st.code) = 'pending'
                      AND LOWER(ot.code) = 'limit'
                      AND (
                            (LOWER(at.code) = 'buy'  AND o.price >= @CurrentPrice)
                         OR (LOWER(at.code) = 'sell' AND o.price <= @CurrentPrice)
                          );
                    """;

                var orders = (await connection.QueryAsync<PendingLimitOrder>(
                    findSql,
                    new { Symbol = symbol, CurrentPrice = currentPrice }
                )).ToList();

                if (orders.Count == 0)
                    return;

                var filledStatusId = await GetFilledStatusIdAsync(connection);
                if (!filledStatusId.HasValue)
                {
                    _logger.LogWarning("No filled/executed status found in lookup table — cannot execute orders.");
                    return;
                }

                foreach (var order in orders)
                    await ExecuteOrderAsync(connection, order, filledStatusId.Value, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Order matching error for {Symbol} @ {Price}", symbol, currentPrice);
            }
        }

        private async Task ExecuteOrderAsync(
            NpgsqlConnection connection,
            PendingLimitOrder order,
            int filledStatusId,
            CancellationToken ct)
        {
            var executionPrice = order.LimitPrice;
            var tradeCost = order.Quantity * executionPrice;
            var walletDelta = order.ActionType == "buy" ? -tradeCost : tradeCost;
            var positionDelta = order.ActionType == "buy" ? order.Quantity : -order.Quantity;

            await using var tx = await connection.BeginTransactionAsync(ct);
            try
            {
                // Mark order filled — the WHERE on status prevents double-execution
                const string fillOrderSql = """
                    UPDATE public.orders
                    SET status_id          = @FilledStatusId,
                        filled_quantity    = quantity,
                        remaining_quantity = 0,
                        execution_price    = @ExecutionPrice,
                        average_fill_price = @ExecutionPrice,
                        updated_at         = CURRENT_TIMESTAMP
                    WHERE id = @OrderId
                      AND status_id = (
                            SELECT id FROM public.lookup
                            WHERE LOWER(code) = 'pending'
                            LIMIT 1
                          );
                    """;

                var rows = await connection.ExecuteAsync(
                    fillOrderSql,
                    new { FilledStatusId = filledStatusId, ExecutionPrice = executionPrice, OrderId = order.Id },
                    tx);

                if (rows == 0)
                {
                    await tx.RollbackAsync(ct);
                    return;
                }

                // Update wallet balance
                const string updateWalletSql = """
                    UPDATE public.wallets
                    SET balance           = balance           + @Delta,
                        available_balance = available_balance + @Delta,
                        updated_at        = CURRENT_TIMESTAMP
                    WHERE user_id = @UserId;
                    """;

                await connection.ExecuteAsync(
                    updateWalletSql,
                    new { Delta = walletDelta, UserId = order.UserId },
                    tx);

                // Update position: buy uses upsert (may not exist yet), sell uses update only
                if (order.ActionType == "buy")
                {
                    const string upsertPositionSql = """
                        INSERT INTO public.positions (user_id, instrument_id, quantity)
                        VALUES (@UserId, @InstrumentId, @Delta)
                        ON CONFLICT (user_id, instrument_id)
                        DO UPDATE SET quantity = public.positions.quantity + EXCLUDED.quantity;
                        """;

                    await connection.ExecuteAsync(
                        upsertPositionSql,
                        new { UserId = order.UserId, InstrumentId = order.InstrumentId, Delta = positionDelta },
                        tx);
                }
                else
                {
                    // Sell: reduce position, guard against going below zero, remove row if fully closed
                    const string updatePositionSql = """
                        UPDATE public.positions
                        SET quantity = quantity + @Delta
                        WHERE user_id = @UserId
                          AND instrument_id = @InstrumentId
                          AND quantity + @Delta >= 0;

                        DELETE FROM public.positions
                        WHERE user_id = @UserId
                          AND instrument_id = @InstrumentId
                          AND quantity = 0;
                        """;

                    await connection.ExecuteAsync(
                        updatePositionSql,
                        new { UserId = order.UserId, InstrumentId = order.InstrumentId, Delta = positionDelta },
                        tx);
                }

                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Limit order {OrderId} filled: {Action} {Qty} {Symbol} @ {Price}",
                    order.Id, order.ActionType, order.Quantity, order.InstrumentId, executionPrice);

                await _hubContext.Clients
                    .Group(GroupNames.ForUser(order.UserId))
                    .SendAsync("OrderFilled", new
                    {
                        orderId = order.Id,
                        status = "EXECUTED",
                        executionPrice,
                        canCancel = false
                    }, ct);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to fill order {OrderId}", order.Id);
            }
        }

        private static async Task<int?> GetFilledStatusIdAsync(NpgsqlConnection connection)
        {
            const string sql = """
                SELECT l.id
                FROM public.lookup l
                INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
                WHERE LOWER(lg.code) IN ('order_status', 'status')
                  AND LOWER(l.code) IN ('filled', 'executed', 'complete', 'completed', 'done')
                  AND COALESCE(l.is_deleted, false) = false
                ORDER BY l.id
                LIMIT 1;
                """;

            return await connection.ExecuteScalarAsync<int?>(sql);
        }

        private sealed class PendingLimitOrder
        {
            public int Id { get; init; }
            public int UserId { get; init; }
            public int InstrumentId { get; init; }
            public decimal Quantity { get; init; }
            public decimal LimitPrice { get; init; }
            public string ActionType { get; init; } = "";
        }
    }
}
