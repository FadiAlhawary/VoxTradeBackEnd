namespace VoxTrade.MarketHubs
{
    public static class SymbolNormalizer
    {
        public static string Normalize(string symbol)
        {
            return symbol.Trim().ToUpperInvariant();
        }
    }
}
