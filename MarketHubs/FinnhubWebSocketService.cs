using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using VoxTrade.Models.DTO;

namespace VoxTrade.MarketHubs
{
 

    public class FinnhubWebSocketService : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly IHubContext<MarketHub> _hubContext;
        private readonly ILogger<FinnhubWebSocketService> _logger;
        private readonly HashSet<string> _activeSymbols = new();
        private readonly object _lock = new();

        private ClientWebSocket? _socket;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public FinnhubWebSocketService(
            IConfiguration configuration,
            IHubContext<MarketHub> hubContext,
            ILogger<FinnhubWebSocketService> logger)
        {
            _configuration = configuration;
            _hubContext = hubContext;
            _logger = logger;
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Finnhub websocket failed. Reconnecting...");
                }

                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
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

            // 🔥 re-subscribe everything after reconnect
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
                await HandleFinnhubMessageAsync(json, ct);

                Console.WriteLine("Listening...");
                Console.WriteLine("RAW: " + json);
            }
        }

        private async Task HandleFinnhubMessageAsync(string json, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();

            if (!string.Equals(type, "trade", StringComparison.OrdinalIgnoreCase))
                return;

            if (!root.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
                return;

            foreach (var item in dataProp.EnumerateArray())
            {
                var symbol = item.GetProperty("s").GetString();
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

                await _hubContext.Clients
                    .Group(GroupNames.ForSymbol(dto.Symbol))
                    .SendAsync("MarketTick", dto, ct);
            }
        }
    }
}
