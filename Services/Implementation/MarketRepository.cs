using Dapper;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.MarketHubs;
using VoxTrade.Models.DTO;
using VoxTrade.Services.Interface;

namespace VoxTrade.Services.Implementation
{
    public class MarketRepository : IMarketRepository
    {
        private readonly ILogger<MarketRepository> _logger;
        private readonly TradingDbContext _context;
        private readonly IHubContext<MarketHub> _hubContext;

        public MarketRepository(ILogger<MarketRepository> logger, TradingDbContext TradingDbContext, IHubContext<MarketHub> hubContext)
        {
            _logger = logger;
            _context = TradingDbContext;
            _hubContext = hubContext;
        }

        public async Task<PlaceOrderResponseDto> PlaceOrder(PlaceOrderRequestDto request)
        {
            var validationError = ValidateRequest(request);
            if (validationError is not null)
                return validationError;

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var instrumentId = request.InstrumentId;
            if (instrumentId <= 0 && !string.IsNullOrWhiteSpace(request.Symbol))
            {
                var resolvedInstrumentId = await ResolveOrCreateInstrumentIdAsync(connection, request.Symbol);
                if (!resolvedInstrumentId.HasValue)
                    return Rejected("Unable to resolve instrument for symbol.");
                instrumentId = resolvedInstrumentId.Value;
            }

            if (instrumentId <= 0)
                return Rejected("A valid instrument is required to place an order.");

            var referencePrice = await ResolveReferencePriceAsync(connection, instrumentId, request);
            if (!referencePrice.HasValue || referencePrice <= 0)
            {
                return Rejected(request.OrderType.Equals("limit", StringComparison.OrdinalIgnoreCase)
                    ? "Limit price must be greater than 0."
                    : "Live market price is unavailable for this instrument. Please try a limit order.");
            }

            if (request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase))
            {
                var availableCash = await GetAvailableCashAsync(connection, request.UserId);
                var estimatedCost = decimal.Round(request.Quantity * referencePrice.Value, 4, MidpointRounding.AwayFromZero);
                if (estimatedCost > availableCash)
                    return Rejected($"Insufficient available cash. Required {estimatedCost:N2}, available {availableCash:N2}.");
            }

            if (request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase))
            {
                var availableToSell = await GetAvailableSellQuantityAsync(connection, request.UserId, instrumentId);
                if (request.Quantity > availableToSell)
                    return Rejected($"Insufficient holdings. Requested {request.Quantity:N4}, available to sell {availableToSell:N4}.");
            }

            var orderTypeId = await GetLookupIdAsync(connection, new[] { "order_type" }, new[] { request.OrderType });
            var actionTypeId = await GetLookupIdAsync(connection, new[] { "order_action" }, new[] { request.Side });
            var statusId = await GetLookupIdAsync(connection, new[] { "order_status" }, new[] { "pending", "new" });
            var sourceId = await GetLookupIdAsync(connection, new[] { "order_source", "source" }, new[] { "manual", "MANUAL" });

            if (!orderTypeId.HasValue || !actionTypeId.HasValue || !statusId.HasValue || !sourceId.HasValue)
                return Rejected("Order configuration is incomplete. Please contact support.");

            var isLimit = request.OrderType.Equals("limit", StringComparison.OrdinalIgnoreCase);

            const string insertSql = """
                INSERT INTO public.orders
                    (user_id, instrument_id, order_type_id, action_type_id, quantity, price, limit_price, status_id, source_id, created_at, updated_at, remaining_quantity)
                VALUES
                    (@UserId, @InstrumentId, @OrderTypeId, @ActionTypeId, @Quantity, @Price, @LimitPrice, @StatusId, @SourceId, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @Quantity)
                RETURNING id;
                """;

            var orderId = await connection.ExecuteScalarAsync<int>(insertSql, new
            {
                UserId = request.UserId,
                InstrumentId = instrumentId,
                OrderTypeId = orderTypeId.Value,
                ActionTypeId = actionTypeId.Value,
                Quantity = request.Quantity,
                Price = referencePrice.Value,
                LimitPrice = isLimit ? referencePrice : (decimal?)null,
                StatusId = statusId.Value,
                SourceId = sourceId.Value,
            });

            var instrumentName = await connection.ExecuteScalarAsync<string?>(
                "SELECT name FROM public.instruments WHERE id = @Id LIMIT 1",
                new { Id = instrumentId });

            // Market orders execute immediately; limit orders wait for a price tick.
            var finalStatus = "PENDING";
            decimal? finalExecutionPrice = null;
            if (!isLimit)
            {
                var executed = await ExecuteMarketOrderAsync(
                    connection, orderId, request.UserId, instrumentId,
                    request.Side, request.Quantity, referencePrice.Value);
                if (executed)
                {
                    finalStatus = "EXECUTED";
                    finalExecutionPrice = referencePrice.Value;
                }
            }

            await _hubContext.Clients
                .Group(GroupNames.ForUser(request.UserId))
                .SendAsync("OrderPlaced", new
                {
                    id = orderId,
                    symbol = request.Symbol,
                    instrumentName,
                    orderType = request.OrderType,
                    actionType = request.Side,
                    status = finalStatus,
                    source = "MANUAL",
                    quantity = request.Quantity,
                    price = isLimit ? referencePrice.Value : (finalExecutionPrice ?? referencePrice.Value),
                    limitPrice = isLimit ? referencePrice.Value : (decimal?)null,
                    stopPrice = (decimal?)null,
                    executionPrice = finalExecutionPrice,
                    averageFillPrice = finalExecutionPrice,
                    filledQuantity = finalStatus == "EXECUTED" ? request.Quantity : (decimal?)null,
                    createdAt = DateTime.UtcNow.ToString("o"),
                    canCancel = finalStatus == "PENDING"
                });

            return new PlaceOrderResponseDto
            {
                Success = true,
                Message = $"{request.Side.ToUpperInvariant()} order placed successfully.",
                OrderId = orderId,
                Status = finalStatus,
                Price = referencePrice.Value
            };
        }

        private static PlaceOrderResponseDto? ValidateRequest(PlaceOrderRequestDto request)
        {
            if (request.UserId <= 0)
            {
                return Rejected("A valid user is required.");
            }

            if (request.Quantity <= 0)
            {
                return Rejected("Quantity must be greater than 0.");
            }

            if (!request.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
                && !request.Side.Equals("sell", StringComparison.OrdinalIgnoreCase))
            {
                return Rejected("Side must be either 'buy' or 'sell'.");
            }

            if (!request.OrderType.Equals("market", StringComparison.OrdinalIgnoreCase)
                && !request.OrderType.Equals("limit", StringComparison.OrdinalIgnoreCase))
            {
                return Rejected("Order type must be either 'market' or 'limit'.");
            }

            if (request.OrderType.Equals("limit", StringComparison.OrdinalIgnoreCase)
                && (!request.LimitPrice.HasValue || request.LimitPrice.Value <= 0))
            {
                return Rejected("Limit price must be greater than 0.");
            }

            return null;
        }

        private static PlaceOrderResponseDto Rejected(string message)
        {
            return new PlaceOrderResponseDto
            {
                Success = false,
                Message = message,
                Status = "REJECTED"
            };
        }

        private async Task<bool> ExecuteMarketOrderAsync(
            System.Data.Common.DbConnection connection,
            int orderId,
            int userId,
            int instrumentId,
            string side,
            decimal quantity,
            decimal executionPrice)
        {
            var filledStatusId = await GetFilledStatusIdAsync(connection);
            if (!filledStatusId.HasValue)
            {
                _logger.LogWarning("No filled status found in lookup — market order {OrderId} left as PENDING.", orderId);
                return false;
            }

            var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
            var tradeCost = quantity * executionPrice;
            var walletDelta = isBuy ? -tradeCost : tradeCost;
            var positionDelta = isBuy ? quantity : -quantity;

            await using var tx = await connection.BeginTransactionAsync();
            try
            {
                await connection.ExecuteAsync("""
                    UPDATE public.orders
                    SET status_id          = @FilledStatusId,
                        filled_quantity    = quantity,
                        remaining_quantity = 0,
                        execution_price    = @ExecutionPrice,
                        average_fill_price = @ExecutionPrice,
                        updated_at         = CURRENT_TIMESTAMP
                    WHERE id = @OrderId;
                    """,
                    new { FilledStatusId = filledStatusId.Value, ExecutionPrice = executionPrice, OrderId = orderId },
                    tx);

                await connection.ExecuteAsync("""
                    UPDATE public.wallets
                    SET balance           = balance           + @Delta,
                        available_balance = available_balance + @Delta,
                        updated_at        = CURRENT_TIMESTAMP
                    WHERE user_id = @UserId;
                    """,
                    new { Delta = walletDelta, UserId = userId },
                    tx);

                if (isBuy)
                {
                    await connection.ExecuteAsync("""
                        INSERT INTO public.positions (user_id, instrument_id, quantity)
                        VALUES (@UserId, @InstrumentId, @Delta)
                        ON CONFLICT (user_id, instrument_id)
                        DO UPDATE SET quantity = public.positions.quantity + EXCLUDED.quantity;
                        """,
                        new { UserId = userId, InstrumentId = instrumentId, Delta = positionDelta },
                        tx);
                }
                else
                {
                    await connection.ExecuteAsync("""
                        UPDATE public.positions
                        SET quantity = quantity + @Delta
                        WHERE user_id = @UserId
                          AND instrument_id = @InstrumentId
                          AND quantity + @Delta >= 0;

                        DELETE FROM public.positions
                        WHERE user_id = @UserId
                          AND instrument_id = @InstrumentId
                          AND quantity = 0;
                        """,
                        new { UserId = userId, InstrumentId = instrumentId, Delta = positionDelta },
                        tx);
                }

                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to execute market order {OrderId}", orderId);
                return false;
            }
        }

        private static async Task<int?> GetFilledStatusIdAsync(System.Data.Common.DbConnection connection)
        {
            return await connection.ExecuteScalarAsync<int?>("""
                SELECT l.id
                FROM public.lookup l
                INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
                WHERE LOWER(lg.code) IN ('order_status', 'status')
                  AND LOWER(l.code) IN ('filled', 'executed', 'complete', 'completed', 'done')
                  AND COALESCE(l.is_deleted, false) = false
                ORDER BY l.id
                LIMIT 1;
                """);
        }

        private async Task<decimal> GetAvailableCashAsync(System.Data.Common.DbConnection connection, int userId)
        {
            const string sql = """
SELECT COALESCE(available_balance, 0)
FROM public.wallets
WHERE user_id = @UserId
ORDER BY id
LIMIT 1;
""";

            return await connection.ExecuteScalarAsync<decimal>(sql, new { UserId = userId });
        }

        private async Task<decimal> GetAvailableSellQuantityAsync(System.Data.Common.DbConnection connection, int userId, int instrumentId)
        {
            const string sql = """
SELECT GREATEST(
    COALESCE((
        SELECT SUM(COALESCE(quantity, 0))
        FROM public.positions
        WHERE user_id = @UserId AND instrument_id = @InstrumentId
    ), 0)
    -
    COALESCE((
        SELECT SUM(COALESCE(o.remaining_quantity, GREATEST(COALESCE(o.quantity, 0) - COALESCE(o.filled_quantity, 0), 0)))
        FROM public.orders o
        LEFT JOIN public.lookup st ON st.id = o.status_id
        LEFT JOIN public.lookup at ON at.id = o.action_type_id
        WHERE o.user_id = @UserId
          AND o.instrument_id = @InstrumentId
          AND LOWER(COALESCE(at.code, '')) = 'sell'
          AND LOWER(COALESCE(st.code, '')) IN ('pending', 'partially_filled')
    ), 0),
    0
);
""";

            return await connection.ExecuteScalarAsync<decimal>(sql, new { UserId = userId, InstrumentId = instrumentId });
        }

        private async Task<int?> GetLookupIdAsync(System.Data.Common.DbConnection connection, string[] groupCodes, string[] preferredCodes)
        {
            const string byCodeSql = """
                SELECT l.id
                FROM public.lookup l
                INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
                WHERE lg.code = ANY(@GroupCodes)
                  AND LOWER(l.code) = LOWER(@Code)
                  AND COALESCE(l.is_deleted, false) = false
                ORDER BY l.id
                LIMIT 1;
                """;

            foreach (var code in preferredCodes)
            {
                var id = await connection.ExecuteScalarAsync<int?>(byCodeSql, new { GroupCodes = groupCodes, Code = code });
                if (id.HasValue)
                    return id.Value;
            }

            const string fallbackSql = """
                SELECT l.id
                FROM public.lookup l
                INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
                WHERE lg.code = ANY(@GroupCodes)
                  AND COALESCE(l.is_deleted, false) = false
                ORDER BY l.id
                LIMIT 1;
                """;

            return await connection.ExecuteScalarAsync<int?>(fallbackSql, new { GroupCodes = groupCodes });
        }

        private async Task<decimal?> ResolveReferencePriceAsync(
            System.Data.Common.DbConnection connection,
            int instrumentId,
            PlaceOrderRequestDto request)
        {
            if (request.OrderType.Equals("limit", StringComparison.OrdinalIgnoreCase))
            {
                return request.LimitPrice;
            }

            const string sql = """
SELECT COALESCE(last_trade_price, ask_price, bid_price)
FROM public.market_quotes
WHERE instrument_id = @InstrumentId
ORDER BY quote_timestamp DESC
LIMIT 1;
""";

            return await connection.ExecuteScalarAsync<decimal?>(sql, new { InstrumentId = instrumentId });
        }

        private async Task<int?> ResolveOrCreateInstrumentIdAsync(System.Data.Common.DbConnection connection, string symbol)
        {
            var normalized = symbol.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            const string findSql = """
SELECT i.id
FROM public.instruments i
WHERE COALESCE(i.is_deleted, false) = false
  AND (
        UPPER(i.symbol) = @Normalized
        OR UPPER(i.short_name) = @Normalized
      )
ORDER BY
    CASE WHEN UPPER(i.symbol) = @Normalized THEN 0 ELSE 1 END,
    i.id
LIMIT 1;
""";

            var existingId = await connection.ExecuteScalarAsync<int?>(findSql, new { Normalized = normalized });
            if (existingId.HasValue)
                return existingId.Value;

            var shortName = normalized.Contains(':')
                ? normalized.Split(':', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? normalized
                : normalized;

            const string insertSql = """
INSERT INTO public.instruments (
    symbol,
    name,
    short_name,
    tick_size,
    min_quantity,
    instrument_type,
    status,
    is_deleted
)
VALUES (
    @Symbol,
    @Name,
    @ShortName,
    0.0001,
    0.000001,
    (
        SELECT l.id
        FROM public.lookup l
        INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
        WHERE lg.code = 'instrument_type' AND l.code = 'stock'
        LIMIT 1
    ),
    (
        SELECT l.id
        FROM public.lookup l
        INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
        WHERE lg.code = 'instrument_status' AND l.code = 'active'
        LIMIT 1
    ),
    false
)
RETURNING id;
""";

            return await connection.ExecuteScalarAsync<int?>(
                insertSql,
                new
                {
                    Symbol = normalized,
                    Name = shortName,
                    ShortName = shortName
                });
        }
    }
}
