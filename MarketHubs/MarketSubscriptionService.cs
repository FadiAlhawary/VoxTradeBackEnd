using System.Collections.Concurrent;

namespace VoxTrade.MarketHubs
{
   

    public class MarketSubscriptionService
    {
        private readonly FinnhubWebSocketService _finnhub;

        // connectionId -> symbols
        private readonly ConcurrentDictionary<string, HashSet<string>> _connectionSymbols = new();

        // symbol -> subscriber count
        private readonly ConcurrentDictionary<string, int> _symbolCounts = new();

        private readonly object _lock = new();

        public MarketSubscriptionService(FinnhubWebSocketService finnhub)
        {
            _finnhub = finnhub;
        }

        public Task AddSubscriptionAsync(string connectionId, string symbol)
        {
            lock (_lock)
            {
                if (!_connectionSymbols.TryGetValue(connectionId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _connectionSymbols[connectionId] = set;
                }

                if (set.Add(symbol))
                {
                    var count = _symbolCounts.AddOrUpdate(symbol, 1, (_, current) => current + 1);
                    if (count == 1)
                    {
                        _ = _finnhub.SubscribeAsync(symbol);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveSubscriptionAsync(string connectionId, string symbol)
        {
            lock (_lock)
            {
                if (_connectionSymbols.TryGetValue(connectionId, out var set) && set.Remove(symbol))
                {
                    var count = _symbolCounts.AddOrUpdate(symbol, 0, (_, current) => Math.Max(0, current - 1));
                    if (count == 0)
                    {
                        _symbolCounts.TryRemove(symbol, out _);
                        _ = _finnhub.UnsubscribeAsync(symbol);
                    }

                    if (set.Count == 0)
                    {
                        _connectionSymbols.TryRemove(connectionId, out _);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveAllForConnectionAsync(string connectionId)
        {
            lock (_lock)
            {
                if (!_connectionSymbols.TryRemove(connectionId, out var symbols))
                    return Task.CompletedTask;

                foreach (var symbol in symbols)
                {
                    var count = _symbolCounts.AddOrUpdate(symbol, 0, (_, current) => Math.Max(0, current - 1));
                    if (count == 0)
                    {
                        _symbolCounts.TryRemove(symbol, out _);
                        _ = _finnhub.UnsubscribeAsync(symbol);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
