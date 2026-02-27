using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;

namespace VoxTradeBackEnd.Services
{
    public class FinnhubWebSocketService : BackgroundService
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<hubs.MarketHub> _hubContext;
        private ClientWebSocket _socket;

        public FinnhubWebSocketService(IConfiguration config, IHubContext<hubs.MarketHub> hubContext)
        {
            _config = config;
            _hubContext = hubContext;
            _socket = new ClientWebSocket();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var token = _config["FinnHub:ApiKey"];
            var uri = new Uri($"wss://ws.finnhub.io?token={token}");

            await _socket.ConnectAsync(uri, stoppingToken);

            // Subscribe to symbols (add what you want)
            await Subscribe("AAPL", stoppingToken);
            await Subscribe("BINANCE:BTCUSDT", stoppingToken);

            var buffer = new byte[4096];

            while (!stoppingToken.IsCancellationRequested)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), stoppingToken);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // Broadcast to all connected frontend clients
                    await _hubContext.Clients.All.SendAsync("ReceiveMarketData", message);
                }
            }
        }

        public async Task Subscribe(string symbol, CancellationToken token)
        {
            var msg = $"{{\"type\":\"subscribe\",\"symbol\":\"{symbol}\"}}";
            var bytes = Encoding.UTF8.GetBytes(msg);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
        }
    }
}