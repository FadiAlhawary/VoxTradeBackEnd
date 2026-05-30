using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using VoxTrade.Models.DTO;
using Dapper;
using Npgsql;

namespace VoxTrade.MarketHubs
{
 

    public class FinnhubWebSocketService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<MarketHub> _hubContext;
        private readonly ILogger<FinnhubWebSocketService> _logger;
        private readonly OrderMatchingService _orderMatching;
        private readonly HashSet<string> _activeSymbols = new();
        private readonly object _lock = new();

        private ClientWebSocket? _socket;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly string _connectionString;

        public FinnhubWebSocketService(
            IConfiguration configuration,
            IHubContext<MarketHub> hubContext,
            ILogger<FinnhubWebSocketService> logger,
            OrderMatchingService orderMatching)
        {
            _configuration = configuration;
            _hubContext = hubContext;
            _logger = logger;
            _orderMatching = orderMatching;

            _connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("DefaultConnection missing.");
        }
        private async Task UpsertMarketQuoteAsync(MarketTickDto dto, CancellationToken ct)
        {
            const string sql = """
        SELECT public.upsert_market_quote(
            i.id,
            NULL,
            NULL,
            @LastTradePrice,
            'finnhub'
        )
        FROM public.instruments i
        WHERE UPPER(i.symbol) = UPPER(@Symbol)
          AND COALESCE(i.is_deleted, false) = false;
    """;

            await using var connection = new NpgsqlConnection(_connectionString);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        Symbol = dto.Symbol,
                        LastTradePrice = dto.Price
                    },
                    cancellationToken: ct
                )
            );
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ConnectAsync(stoppingToken);
                    await ReceiveLoopAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Finnhub websocket failed. Reconnecting...");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            _socket?.Dispose();
            _socket = new ClientWebSocket();

            var apiKey = _configuration["Finnhub:ApiKey"]
                         ?? throw new InvalidOperationException("Finnhub API key missing.");

            var uri = new Uri($"wss://ws.finnhub.io?token={apiKey}");
            await _socket.ConnectAsync(uri, ct);

            _logger.LogInformation("Connected to Finnhub websocket.");
            Console.WriteLine("Connected to Finnhub");

            List<string> symbols;
            lock (_lock)
            {
                symbols = _activeSymbols.ToList();
            }

            foreach (var symbol in symbols)
            {
                await SendAsync(new { type = "subscribe", symbol });
                Console.WriteLine($"Re-subscribed: {symbol}");
            }
        }

        public async Task SubscribeAsync(string symbol)
        {
            lock (_lock)
            {
                _activeSymbols.Add(symbol);
            }

            await SendAsync(new { type = "subscribe", symbol });
        }

        public async Task UnsubscribeAsync(string symbol)
        {
            lock (_lock)
            {
                _activeSymbols.Remove(symbol);
            }

            await SendAsync(new { type = "unsubscribe", symbol });
        }

        private async Task SendAsync(object payload)
        {
            if (_socket is null || _socket.State != WebSocketState.Open)
                return;

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            await _sendLock.WaitAsync();
            try
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[1024 * 16];

            while (_socket is not null && _socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                try
                {
                    var ms = new MemoryStream();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", ct);
                            return;
                        }

                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    Console.WriteLine("Listening...");
                    Console.WriteLine("RAW: " + json);
                    
                    await HandleFinnhubMessageAsync(json, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in receive loop");
                    return;
                }
            }
        }

        private async Task HandleFinnhubMessageAsync(string json, CancellationToken ct)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp))
                    return;

                var type = typeProp.GetString();

                // Skip non-trade messages (e.g., pings, subscription confirmations)
                if (!string.Equals(type, "trade", StringComparison.OrdinalIgnoreCase))
                    return;

                if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
                    return;

                foreach (var item in dataProp.EnumerateArray())
                {
                    if (!item.TryGetProperty("s", out var symbolProp))
                        continue;

                    var symbol = symbolProp.GetString();
                    if (string.IsNullOrWhiteSpace(symbol))
                        continue;

                    var dto = new MarketTickDto
                    {
                        Symbol = symbol,
                        Price = item.TryGetProperty("p", out var p) ? p.GetDecimal() : 0,
                        Volume = item.TryGetProperty("v", out var v) ? v.GetDecimal() : 0,
                        TimestampUnixMs = item.TryGetProperty("t", out var t) ? t.GetInt64() : 0,
                        Conditions = item.TryGetProperty("c", out var c) && c.ValueKind == JsonValueKind.Array
                            ? c.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                            : Array.Empty<string>()
                    };

                    await UpsertMarketQuoteAsync(dto, ct);
                    await _orderMatching.MatchOrdersForSymbolAsync(dto.Symbol, dto.Price, ct);

                    await _hubContext.Clients
                        .Group(GroupNames.ForSymbol(dto.Symbol))
                        .SendAsync("MarketTick", dto, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling Finnhub message: {Message}", json);
            }
        }
    }
}
