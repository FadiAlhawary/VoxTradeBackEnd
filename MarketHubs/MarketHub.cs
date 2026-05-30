using Microsoft.AspNetCore.SignalR;
using VoxTrade.MarketHubs;

namespace VoxTrade.MarketHubs
{
    public class MarketHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly MarketSubscriptionService _subscriptions;

        public MarketHub(MarketSubscriptionService subscriptions)
        {
            _subscriptions = subscriptions;
        }

        public async Task SubscribeSymbol(string symbol)
        {
            var normalized = SymbolNormalizer.Normalize(symbol);
            var group = GroupNames.ForSymbol(normalized);

            await Groups.AddToGroupAsync(Context.ConnectionId, group);
            await _subscriptions.AddSubscriptionAsync(Context.ConnectionId, normalized);
        }

        public async Task UnsubscribeSymbol(string symbol)
        {
            var normalized = SymbolNormalizer.Normalize(symbol);
            var group = GroupNames.ForSymbol(normalized);

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
            await _subscriptions.RemoveSubscriptionAsync(Context.ConnectionId, normalized);
        }

        public async Task SubscribeOrders(int userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.ForUser(userId));
        }

        public async Task UnsubscribeOrders(int userId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNames.ForUser(userId));
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await _subscriptions.RemoveAllForConnectionAsync(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
