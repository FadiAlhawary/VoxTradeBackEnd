namespace VoxTrade.MarketHubs
{
    public static class GroupNames
    {
        public static string ForSymbol(string symbol) => $"market:{symbol}";
        public static string ForUser(int userId) => $"orders:user:{userId}";
    }
}
