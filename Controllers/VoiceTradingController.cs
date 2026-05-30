using System.Data;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using VoxTrade.Api.Data;
using VoxTrade.MarketHubs;
using VoxTrade.Models.DTO;

namespace VoxTrade.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VoiceTradingController : ControllerBase
    {
        private readonly TradingDbContext _context;
        private readonly ILogger<VoiceTradingController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<MarketHub> _hubContext;

        public VoiceTradingController(TradingDbContext context, ILogger<VoiceTradingController> logger, IConfiguration configuration, IHubContext<MarketHub> hubContext)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        [HttpPost("validate-instrument")]
        public async Task<ActionResult<VoiceInstrumentValidationResponse>> ValidateInstrument([FromBody] VoiceInstrumentValidationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new VoiceInstrumentValidationResponse
                {
                    Exists = false,
                    Message = "Symbol is required."
                });
            }

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var instrument = await FindInstrument(connection, null, request.Symbol.Trim());

                if (instrument == null)
                {
                    return Ok(new VoiceInstrumentValidationResponse
                    {
                        Exists = false,
                        Message = "No such stock/crypto was found in instruments."
                    });
                }

                return Ok(new VoiceInstrumentValidationResponse
                {
                    Exists = true,
                    InstrumentId = instrument.Id,
                    Symbol = instrument.Symbol,
                    Name = instrument.Name,
                    Message = "Instrument exists."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate instrument for symbol {Symbol}", request.Symbol);
                return StatusCode(500, new VoiceInstrumentValidationResponse
                {
                    Exists = false,
                    Message = "Failed to validate instrument."
                });
            }
        }

        [HttpPost("execute")]
        public async Task<ActionResult<ExecuteVoiceOrderResponse>> Execute([FromBody] ExecuteVoiceOrderRequest request)
        {
            if (request == null)
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Request body is required." });
            }

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new ExecuteVoiceOrderResponse { Success = false, Message = "Invalid user token." });
            }

            var normalizedIntent = (request.Intent ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedIntent != "buy" && normalizedIntent != "sell")
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Intent must be buy or sell." });
            }

            if (string.IsNullOrWhiteSpace(request.Symbol))
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Symbol is required." });
            }

            if (!request.Quantity.HasValue || request.Quantity.Value <= 0)
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Quantity must be greater than zero." });
            }

            var normalizedOrderType = (request.OrderType ?? "market").Trim().ToLowerInvariant();
            if (normalizedOrderType != "market" && normalizedOrderType != "limit" && normalizedOrderType != "stop")
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Order type must be market, limit, or stop." });
            }

            if (normalizedOrderType == "limit" && (!request.Price.HasValue || request.Price.Value <= 0))
            {
                return BadRequest(new ExecuteVoiceOrderResponse { Success = false, Message = "Limit orders require a valid price." });
            }

            try
            {
                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                var instrument = await FindInstrument(connection, null, request.Symbol.Trim());
                if (instrument == null)
                {
                    return NotFound(new ExecuteVoiceOrderResponse
                    {
                        Success = false,
                        Message = "No such stock/crypto was found in instruments."
                    });
                }

                var resolvedPrice = await ResolveExecutionPriceAsync(
                    connection,
                    null,
                    instrument.Id,
                    instrument.Symbol,
                    normalizedIntent,
                    normalizedOrderType,
                    request.Price);

                if (!resolvedPrice.HasValue || resolvedPrice.Value <= 0)
                {
                    return BadRequest(new ExecuteVoiceOrderResponse
                    {
                        Success = false,
                        Message = $"Unable to resolve a valid price for {instrument.Symbol}."
                    });
                }

                if (normalizedIntent == "buy")
                {
                    var availableCash = await GetAvailableCashAsync(connection, null, userId);
                    var estimatedCost = decimal.Round(request.Quantity.Value * resolvedPrice.Value, 4, MidpointRounding.AwayFromZero);
                    if (estimatedCost > availableCash)
                    {
                        return BadRequest(new ExecuteVoiceOrderResponse
                        {
                            Success = false,
                            Message = $"Insufficient available cash. Required {estimatedCost:N2}, available {availableCash:N2}."
                        });
                    }
                }

                if (normalizedIntent == "sell")
                {
                    var availableToSell = await GetAvailableSellQuantityAsync(connection, null, userId, instrument.Id);
                    if (request.Quantity.Value > availableToSell)
                    {
                        return BadRequest(new ExecuteVoiceOrderResponse
                        {
                            Success = false,
                            Message = $"Insufficient holdings. Requested {request.Quantity.Value:N4}, available to sell {availableToSell:N4}."
                        });
                    }
                }

                var orderActionId = await GetLookupId(connection, null, new[] { "order_action" }, new[] { normalizedIntent });
                var orderTypeId = await GetLookupId(connection, null, new[] { "order_type" }, new[] { normalizedOrderType });
                var orderStatusId = await GetLookupId(connection, null, new[] { "order_status" }, new[] { "pending", "new" });
                var sourceId = await GetLookupId(
                    connection,
                    null,
                    new[] { "order_source", "source" },
                    new[] { "voice", "VOICE", "voice_order", "voice_trading" });

                if (!orderActionId.HasValue || !orderTypeId.HasValue || !orderStatusId.HasValue || !sourceId.HasValue)
                {
                    return StatusCode(500, new ExecuteVoiceOrderResponse
                    {
                        Success = false,
                        Message = "Lookup configuration is incomplete for order creation."
                    });
                }

                var languageId = await GetLanguageId(connection, null, request.Language);
                var currencyId = await GetCurrencyId(connection, null, request.CurrencySymbol);
                var normalizedSymbol = request.Symbol.Trim().ToUpperInvariant();
                var normalizedRawText = string.IsNullOrWhiteSpace(request.RawText)
                    ? $"{normalizedIntent} {request.Quantity} {normalizedSymbol}"
                    : request.RawText.Trim();

                await using var transaction = await connection.BeginTransactionAsync();

                const string insertVoiceCommandSql = """
                    INSERT INTO public.voice_commands
                        (user_id, raw_text, language_id, confidence_score, recognized_at)
                    VALUES
                        (@UserId, @RawText, @LanguageId, @ConfidenceScore, CURRENT_TIMESTAMP)
                    RETURNING id;
                    """;

                var voiceCommandId = await connection.ExecuteScalarAsync<int>(
                    insertVoiceCommandSql,
                    new
                    {
                        UserId = userId,
                        RawText = normalizedRawText,
                        LanguageId = languageId,
                        ConfidenceScore = request.Confidence
                    },
                    transaction);

                const string insertInterpretationSql = """
                    INSERT INTO public.command_interpretations
                        (voice_command_id, intent, instrument_id, quantity, price, currency_id, parsed_successfully, error_message)
                    VALUES
                        (@VoiceCommandId, @Intent, @InstrumentId, @Quantity, @Price, @CurrencyId, true, NULL);
                    """;

                await connection.ExecuteAsync(
                    insertInterpretationSql,
                    new
                    {
                        VoiceCommandId = voiceCommandId,
                        Intent = normalizedIntent,
                        InstrumentId = instrument.Id,
                        Quantity = request.Quantity,
                        Price = resolvedPrice,
                        CurrencyId = currencyId
                    },
                    transaction);

                const string insertAuditSql = """
                    INSERT INTO public.voice_command_audit
                        (voice_command_id, step, details, created_at)
                    VALUES
                        (@VoiceCommandId, @Step, @Details, CURRENT_TIMESTAMP);
                    """;

                await connection.ExecuteAsync(
                    insertAuditSql,
                    new
                    {
                        VoiceCommandId = voiceCommandId,
                        Step = "instrument_verified",
                        Details = $"Instrument {instrument.Symbol} ({instrument.Id}) verified and order confirmed by user."
                    },
                    transaction);

                const string insertOrderSql = """
                    INSERT INTO public.orders
                        (user_id, instrument_id, order_type_id, action_type_id, quantity, price, status_id, source_id, voice_command_id, created_at, updated_at, remaining_quantity)
                    VALUES
                        (@UserId, @InstrumentId, @OrderTypeId, @ActionTypeId, @Quantity, @Price, @StatusId, @SourceId, @VoiceCommandId, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @RemainingQuantity)
                    RETURNING id;
                    """;

                var orderId = await connection.ExecuteScalarAsync<int>(
                    insertOrderSql,
                    new
                    {
                        UserId = userId,
                        InstrumentId = instrument.Id,
                        OrderTypeId = orderTypeId.Value,
                        ActionTypeId = orderActionId.Value,
                        Quantity = request.Quantity,
                        Price = resolvedPrice,
                        StatusId = orderStatusId.Value,
                        SourceId = sourceId.Value,
                        VoiceCommandId = voiceCommandId,
                        RemainingQuantity = request.Quantity
                    },
                    transaction);

                await connection.ExecuteAsync(
                    insertAuditSql,
                    new
                    {
                        VoiceCommandId = voiceCommandId,
                        Step = "order_created",
                        Details = $"Order {orderId} created from voice command."
                    },
                    transaction);

                await transaction.CommitAsync();

                // Market orders execute immediately — same logic as MarketRepository
                var finalStatus = "PENDING";
                decimal? finalExecutionPrice = null;

                if (normalizedOrderType == "market")
                {
                    var executed = await ExecuteMarketOrderAsync(
                        connection, orderId, userId, instrument.Id,
                        normalizedIntent, request.Quantity.Value, resolvedPrice.Value);

                    if (executed)
                    {
                        finalStatus = "EXECUTED";
                        finalExecutionPrice = resolvedPrice.Value;
                    }
                }

                // Broadcast to the user's SignalR group so the Orders page updates live
                await _hubContext.Clients
                    .Group(GroupNames.ForUser(userId))
                    .SendAsync("OrderPlaced", new
                    {
                        id = orderId,
                        symbol = instrument.Symbol,
                        orderType = normalizedOrderType,
                        actionType = normalizedIntent,
                        status = finalStatus,
                        source = "VOICE",
                        quantity = request.Quantity.Value,
                        price = resolvedPrice.Value,
                        executionPrice = finalExecutionPrice,
                        averageFillPrice = finalExecutionPrice,
                        filledQuantity = finalStatus == "EXECUTED" ? request.Quantity.Value : (decimal?)null,
                        createdAt = DateTime.UtcNow.ToString("o"),
                        canCancel = finalStatus == "PENDING"
                    });

                return Ok(new ExecuteVoiceOrderResponse
                {
                    Success = true,
                    Message = "Voice order executed and saved successfully.",
                    OrderId = orderId,
                    VoiceCommandId = voiceCommandId,
                    InstrumentId = instrument.Id,
                    Symbol = instrument.Symbol,
                    ExecutedPrice = resolvedPrice
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute voice order for symbol {Symbol}", request.Symbol);
                return StatusCode(500, new ExecuteVoiceOrderResponse
                {
                    Success = false,
                    Message = $"Failed to execute voice order: {ex.Message}"
                });
            }
        }

        private async Task<bool> ExecuteMarketOrderAsync(
            DbConnection connection,
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
                _logger.LogWarning("No filled status in lookup — voice market order {OrderId} left as PENDING.", orderId);
                return false;
            }

            var isBuy = side.Equals("buy", StringComparison.OrdinalIgnoreCase);
            var tradeCost    = quantity * executionPrice;
            var walletDelta  = isBuy ? -tradeCost : tradeCost;
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
                        WHERE user_id  = @UserId
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
                _logger.LogError(ex, "Failed to execute voice market order {OrderId}", orderId);
                return false;
            }
        }

        private static Task<int?> GetFilledStatusIdAsync(DbConnection connection) =>
            connection.ExecuteScalarAsync<int?>("""
                SELECT l.id
                FROM public.lookup l
                INNER JOIN public.lookup_group lg ON lg.id = l.lookup_group_id
                WHERE LOWER(lg.code) IN ('order_status', 'status')
                  AND LOWER(l.code) IN ('filled', 'executed', 'complete', 'completed', 'done')
                  AND COALESCE(l.is_deleted, false) = false
                ORDER BY l.id
                LIMIT 1;
                """);

        private bool TryGetUserId(out int userId)
        {
            var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return int.TryParse(claimValue, out userId);
        }

        private static async Task<InstrumentRow?> FindInstrument(DbConnection connection, IDbTransaction? transaction, string query)
        {
            const string sql = """
                SELECT
                    i.id AS Id,
                    i.symbol AS Symbol,
                    i.name AS Name
                FROM public.instruments i
                WHERE COALESCE(i.is_deleted, false) = false
                  AND (
                        UPPER(i.symbol) = UPPER(@Query)
                        OR UPPER(i.name) = UPPER(@Query)
                        OR UPPER(i.symbol) LIKE UPPER(@LikeQuery)
                        OR UPPER(i.name) LIKE UPPER(@LikeQuery)
                      )
                ORDER BY
                    CASE
                        WHEN UPPER(i.symbol) = UPPER(@Query) THEN 0
                        WHEN UPPER(i.name) = UPPER(@Query) THEN 1
                        WHEN UPPER(i.symbol) LIKE UPPER(@LikeQuery) THEN 2
                        ELSE 3
                    END,
                    i.id
                LIMIT 1;
                """;

            foreach (var candidate in BuildInstrumentQueryCandidates(query))
            {
                var instrument = await connection.QueryFirstOrDefaultAsync<InstrumentRow>(
                    sql,
                    new { Query = candidate, LikeQuery = $"%{candidate}%" },
                    transaction);

                if (instrument != null)
                {
                    return instrument;
                }
            }

            return null;
        }

        private static readonly Dictionary<string, string[]> InstrumentAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["BITCOIN"] = new[] { "BTC", "BTCUSD", "BTCUSDT", "BINANCE:BTCUSDT" },
            ["BTC"] = new[] { "BTC", "BTCUSD", "BTCUSDT", "BINANCE:BTCUSDT" },
            ["ETHEREUM"] = new[] { "ETH", "ETHUSD", "ETHUSDT", "BINANCE:ETHUSDT" },
            ["ETHER"] = new[] { "ETH", "ETHUSD", "ETHUSDT", "BINANCE:ETHUSDT" },
            ["ETH"] = new[] { "ETH", "ETHUSD", "ETHUSDT", "BINANCE:ETHUSDT" },
            ["SOLANA"] = new[] { "SOL", "SOLUSD", "SOLUSDT", "BINANCE:SOLUSDT" },
            ["SOL"] = new[] { "SOL", "SOLUSD", "SOLUSDT", "BINANCE:SOLUSDT" },
            ["RIPPLE"] = new[] { "XRP", "XRPUSD", "XRPUSDT", "BINANCE:XRPUSDT" },
            ["DOGECOIN"] = new[] { "DOGE", "DOGEUSD", "DOGEUSDT", "BINANCE:DOGEUSDT" },
            ["CARDANO"] = new[] { "ADA", "ADAUSD", "ADAUSDT", "BINANCE:ADAUSDT" },
            ["BINANCEBTCUSDT"] = new[] { "BINANCE:BTCUSDT", "BTCUSDT", "BTC" },
            ["BINANCEETHUSDT"] = new[] { "BINANCE:ETHUSDT", "ETHUSDT", "ETH" }
        };

        private static IEnumerable<string> BuildInstrumentQueryCandidates(string query)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var normalized = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            if (seen.Add(normalized))
            {
                yield return normalized;
            }

            var upper = normalized.ToUpperInvariant();
            if (seen.Add(upper))
            {
                yield return upper;
            }

            var noSpaces = Regex.Replace(upper, @"\s+", string.Empty);
            if (seen.Add(noSpaces))
            {
                yield return noSpaces;
            }

            var key = Regex.Replace(upper, @"[^A-Z0-9]", string.Empty);
            if (InstrumentAliases.TryGetValue(key, out var aliases))
            {
                foreach (var alias in aliases)
                {
                    if (seen.Add(alias))
                    {
                        yield return alias;
                    }
                }
            }

            if (!upper.Contains(':') && upper.Length >= 3 && upper.Length <= 12)
            {
                var binanceRaw = $"BINANCE:{upper}";
                if (seen.Add(binanceRaw))
                {
                    yield return binanceRaw;
                }

                if (!upper.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
                {
                    var usdt = $"{upper}USDT";
                    if (seen.Add(usdt))
                    {
                        yield return usdt;
                    }

                    var binanceUsdt = $"BINANCE:{upper}USDT";
                    if (seen.Add(binanceUsdt))
                    {
                        yield return binanceUsdt;
                    }
                }
            }
        }

        private static async Task<int?> GetLookupId(DbConnection connection, IDbTransaction? transaction, string[] groupCodes, string[] preferredCodes)
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
                var id = await connection.QueryFirstOrDefaultAsync<int?>(
                    byCodeSql,
                    new { GroupCodes = groupCodes, Code = code },
                    transaction);

                if (id.HasValue)
                {
                    return id.Value;
                }
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

            return await connection.QueryFirstOrDefaultAsync<int?>(
                fallbackSql,
                new { GroupCodes = groupCodes },
                transaction);
        }

        private static Task<decimal> GetAvailableCashAsync(DbConnection connection, IDbTransaction? transaction, int userId)
        {
            const string sql = """
                SELECT COALESCE(available_balance, 0)
                FROM public.wallets
                WHERE user_id = @UserId
                ORDER BY id
                LIMIT 1;
                """;

            return connection.ExecuteScalarAsync<decimal>(sql, new { UserId = userId }, transaction);
        }

        private static Task<decimal> GetAvailableSellQuantityAsync(DbConnection connection, IDbTransaction? transaction, int userId, int instrumentId)
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

            return connection.ExecuteScalarAsync<decimal>(sql, new { UserId = userId, InstrumentId = instrumentId }, transaction);
        }

        private async Task<decimal?> ResolveExecutionPriceAsync(
            DbConnection connection,
            IDbTransaction transaction,
            int instrumentId,
            string symbol,
            string intent,
            string orderType,
            decimal? requestedPrice)
        {
            if (orderType == "limit" && requestedPrice.HasValue && requestedPrice.Value > 0)
            {
                return requestedPrice.Value;
            }

            var marketQuotePrice = await GetLatestMarketQuotePriceAsync(connection, transaction, instrumentId, intent);
            if (marketQuotePrice.HasValue && marketQuotePrice.Value > 0)
            {
                return marketQuotePrice.Value;
            }

            var marketPrice = await GetLatestMarketPriceAsync(connection, transaction, instrumentId, intent);
            if (marketPrice.HasValue && marketPrice.Value > 0)
            {
                return marketPrice.Value;
            }

            var finnhubPrice = await GetFinnhubQuotePriceAsync(symbol);
            if (finnhubPrice.HasValue && finnhubPrice.Value > 0)
            {
                return finnhubPrice.Value;
            }

            if (requestedPrice.HasValue && requestedPrice.Value > 0)
            {
                return requestedPrice.Value;
            }

            return null;
        }

        private static async Task<decimal?> GetLatestMarketQuotePriceAsync(
            DbConnection connection,
            IDbTransaction transaction,
            int instrumentId,
            string intent)
        {
            const string sql = """
                SELECT
                    mq.ask_price AS AskPrice,
                    mq.bid_price AS BidPrice,
                    mq.last_trade_price AS LastTradePrice
                FROM public.market_quotes mq
                WHERE mq.instrument_id = @InstrumentId
                ORDER BY mq.quote_timestamp DESC NULLS LAST, mq.id DESC
                LIMIT 1;
                """;

            MarketQuotePriceRow? quote;
            try
            {
                quote = await connection.QueryFirstOrDefaultAsync<MarketQuotePriceRow>(
                    sql,
                    new { InstrumentId = instrumentId },
                    transaction);
            }
            catch (Exception ex) when (IsMissingRelation(ex))
            {
                return null;
            }

            if (quote == null)
            {
                return null;
            }

            return ChooseBestPrice(intent, quote.AskPrice, quote.BidPrice, quote.LastTradePrice, null);
        }

        private static async Task<decimal?> GetLatestMarketPriceAsync(
            DbConnection connection,
            IDbTransaction transaction,
            int instrumentId,
            string intent)
        {
            const string sql = """
                SELECT
                    mp.ask_price AS AskPrice,
                    mp.bid_price AS BidPrice,
                    mp.price AS LastPrice
                FROM public.market_prices mp
                WHERE mp.instrument_id = @InstrumentId
                ORDER BY mp.price_time DESC NULLS LAST, mp.id DESC
                LIMIT 1;
                """;

            MarketPriceRow? price;
            try
            {
                price = await connection.QueryFirstOrDefaultAsync<MarketPriceRow>(
                    sql,
                    new { InstrumentId = instrumentId },
                    transaction);
            }
            catch (Exception ex) when (IsMissingRelation(ex))
            {
                return null;
            }

            if (price == null)
            {
                return null;
            }

            return ChooseBestPrice(intent, price.AskPrice, price.BidPrice, price.LastPrice, null);
        }

        private async Task<decimal?> GetFinnhubQuotePriceAsync(string symbol)
        {
            var apiKey = _configuration["FinnHub:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            foreach (var candidate in BuildFinnhubSymbolCandidates(symbol))
            {
                try
                {
                    using var httpClient = new HttpClient();
                    var url = $"https://finnhub.io/api/v1/quote?symbol={Uri.EscapeDataString(candidate)}&token={Uri.EscapeDataString(apiKey)}";
                    var response = await httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var quote = JsonSerializer.Deserialize<FinnhubQuoteResponse>(content);
                    if (quote?.CurrentPrice.HasValue == true && quote.CurrentPrice.Value > 0)
                    {
                        return quote.CurrentPrice.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch Finnhub quote for symbol {SymbolCandidate}", candidate);
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildFinnhubSymbolCandidates(string symbol)
        {
            var normalized = (symbol ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return normalized;

            if (!normalized.Contains(':'))
            {
                if (normalized.Length >= 6 && normalized.Length <= 12)
                {
                    yield return $"BINANCE:{normalized}";
                }

                if (!normalized.EndsWith("USDT") && normalized.Length <= 6)
                {
                    yield return $"BINANCE:{normalized}USDT";
                }
            }
        }

        private static decimal? ChooseBestPrice(string intent, decimal? askPrice, decimal? bidPrice, decimal? lastPrice, decimal? fallbackPrice)
        {
            if (string.Equals(intent, "buy", StringComparison.OrdinalIgnoreCase))
            {
                return FirstPositive(askPrice, lastPrice, bidPrice, fallbackPrice);
            }

            if (string.Equals(intent, "sell", StringComparison.OrdinalIgnoreCase))
            {
                return FirstPositive(bidPrice, lastPrice, askPrice, fallbackPrice);
            }

            return FirstPositive(lastPrice, askPrice, bidPrice, fallbackPrice);
        }

        private static bool IsMissingRelation(Exception ex)
        {
            if (ex is PostgresException pgEx)
            {
                return string.Equals(pgEx.SqlState, "42P01", StringComparison.Ordinal);
            }

            return ex.InnerException != null && IsMissingRelation(ex.InnerException);
        }

        private static decimal? FirstPositive(params decimal?[] values)
        {
            foreach (var value in values)
            {
                if (value.HasValue && value.Value > 0)
                {
                    return value.Value;
                }
            }

            return null;
        }

        private static async Task<int?> GetCurrencyId(DbConnection connection, IDbTransaction? transaction, string? currencySymbol)
        {
            if (string.IsNullOrWhiteSpace(currencySymbol))
            {
                currencySymbol = "USD";
            }

            const string sql = """
                SELECT c.id
                FROM public.currencies c
                WHERE UPPER(c.symbol) = UPPER(@Symbol)
                  AND COALESCE(c.is_deleted, false) = false
                LIMIT 1;
                """;

            var result = await connection.QueryFirstOrDefaultAsync<int?>(
                sql,
                new { Symbol = currencySymbol.Trim() },
                transaction);

            if (result.HasValue)
            {
                return result.Value;
            }

            result = await connection.QueryFirstOrDefaultAsync<int?>(
                sql,
                new { Symbol = "USD" },
                transaction);

            if (result.HasValue)
            {
                return result.Value;
            }

            const string fallbackSql = """
                SELECT c.id
                FROM public.currencies c
                WHERE COALESCE(c.is_deleted, false) = false
                ORDER BY c.id
                LIMIT 1;
                """;

            result = await connection.QueryFirstOrDefaultAsync<int?>(
                fallbackSql,
                transaction: transaction);

            return result;
        }

        private sealed class MarketQuotePriceRow
        {
            public decimal? AskPrice { get; init; }
            public decimal? BidPrice { get; init; }
            public decimal? LastTradePrice { get; init; }
        }

        private sealed class MarketPriceRow
        {
            public decimal? AskPrice { get; init; }
            public decimal? BidPrice { get; init; }
            public decimal? LastPrice { get; init; }
        }

        private sealed class FinnhubQuoteResponse
        {
            [JsonPropertyName("c")]
            public decimal? CurrentPrice { get; init; }
        }

        private static async Task<int?> GetLanguageId(DbConnection connection, IDbTransaction? transaction, string? language)
        {
            const string sql = """
                SELECT l.id
                FROM public.languages l
                WHERE LOWER(l.name_en) = LOWER(@Language)
                   OR LOWER(l.name_ar) = LOWER(@Language)
                LIMIT 1;
                """;

            if (!string.IsNullOrWhiteSpace(language))
            {
                var direct = await connection.QueryFirstOrDefaultAsync<int?>(
                    sql,
                    new { Language = language.Trim() },
                    transaction);

                if (direct.HasValue)
                {
                    return direct.Value;
                }
            }

            var fallbackNames = new[] { "english", "en", "arabic", "ar" };
            foreach (var fallbackName in fallbackNames)
            {
                var fallback = await connection.QueryFirstOrDefaultAsync<int?>(
                    sql,
                    new { Language = fallbackName },
                    transaction);

                if (fallback.HasValue)
                {
                    return fallback.Value;
                }
            }

            const string firstRowSql = """
                SELECT l.id
                FROM public.languages l
                ORDER BY l.id
                LIMIT 1;
                """;

            return await connection.QueryFirstOrDefaultAsync<int?>(
                firstRowSql,
                transaction: transaction);
        }

        private sealed class InstrumentRow
        {
            public int Id { get; set; }
            public string Symbol { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
