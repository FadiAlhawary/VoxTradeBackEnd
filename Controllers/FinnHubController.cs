using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Globalization;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    [ApiController]
    public class FinnHubController : ControllerBase
    {
        #region Members
        private readonly IConfiguration _config;
        private readonly ILogger<FinnHubController> _logger;
        private readonly IMemoryCache _cache;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private static readonly List<FinnhubSymbolItem> DefaultForexSymbols = new()
        {
            new FinnhubSymbolItem { Symbol = "OANDA:EUR_USD", DisplaySymbol = "EUR/USD", Description = "Euro / US Dollar", Currency = "USD" },
            new FinnhubSymbolItem { Symbol = "OANDA:GBP_USD", DisplaySymbol = "GBP/USD", Description = "British Pound / US Dollar", Currency = "USD" },
            new FinnhubSymbolItem { Symbol = "OANDA:USD_JPY", DisplaySymbol = "USD/JPY", Description = "US Dollar / Japanese Yen", Currency = "JPY" },
            new FinnhubSymbolItem { Symbol = "OANDA:USD_CHF", DisplaySymbol = "USD/CHF", Description = "US Dollar / Swiss Franc", Currency = "CHF" },
            new FinnhubSymbolItem { Symbol = "OANDA:AUD_USD", DisplaySymbol = "AUD/USD", Description = "Australian Dollar / US Dollar", Currency = "USD" },
            new FinnhubSymbolItem { Symbol = "OANDA:USD_CAD", DisplaySymbol = "USD/CAD", Description = "US Dollar / Canadian Dollar", Currency = "CAD" },
            new FinnhubSymbolItem { Symbol = "OANDA:NZD_USD", DisplaySymbol = "NZD/USD", Description = "New Zealand Dollar / US Dollar", Currency = "USD" },
            new FinnhubSymbolItem { Symbol = "OANDA:EUR_GBP", DisplaySymbol = "EUR/GBP", Description = "Euro / British Pound", Currency = "GBP" },
            new FinnhubSymbolItem { Symbol = "OANDA:EUR_JPY", DisplaySymbol = "EUR/JPY", Description = "Euro / Japanese Yen", Currency = "JPY" }
        };
        private static readonly List<FinnhubSymbolItem> DefaultCryptoSymbols = new()
        {
            new FinnhubSymbolItem { Symbol = "BINANCE:BTCUSDT", DisplaySymbol = "BTC/USDT", Description = "Bitcoin / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:ETHUSDT", DisplaySymbol = "ETH/USDT", Description = "Ethereum / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:SOLUSDT", DisplaySymbol = "SOL/USDT", Description = "Solana / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:BNBUSDT", DisplaySymbol = "BNB/USDT", Description = "BNB / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:XRPUSDT", DisplaySymbol = "XRP/USDT", Description = "XRP / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:ADAUSDT", DisplaySymbol = "ADA/USDT", Description = "Cardano / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:DOGEUSDT", DisplaySymbol = "DOGE/USDT", Description = "Dogecoin / Tether" },
            new FinnhubSymbolItem { Symbol = "BINANCE:AVAXUSDT", DisplaySymbol = "AVAX/USDT", Description = "Avalanche / Tether" }
        };
        #endregion

        #region Constructor
        public FinnHubController(IConfiguration configuration, ILogger<FinnHubController> logger, IMemoryCache cache)
        {
            _config = configuration;
            _logger = logger;
            _cache = cache;
        }
        #endregion

        #region Helper Method
        private async Task<string> FetchFinnhubContent(string baseUrl, Dictionary<string, string?> parameters)
        {
            string key = _config["FinnHub:ApiKey"]
                         ?? throw new InvalidOperationException("Finnhub API key missing.");

            parameters["token"] = key;

            var url = QueryHelpers.AddQueryString(baseUrl, parameters);

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Finnhub returned {(int)response.StatusCode} for {baseUrl}");

            return await response.Content.ReadAsStringAsync();
        }

        private async Task<IActionResult> CallFinnhub(string baseUrl, Dictionary<string, string?> parameters)
        {
            try
            {
                var content = await FetchFinnhubContent(baseUrl, parameters);
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Finnhub API call failed");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<List<FinnhubSymbolItem>> GetCachedSymbolsAsync(
            string cacheKey,
            string baseUrl,
            Dictionary<string, string?> parameters,
            IEnumerable<FinnhubSymbolItem>? fallbackSymbols = null)
        {
            try
            {
                var symbols = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

                    var json = await FetchFinnhubContent(baseUrl, parameters);
                    return JsonSerializer.Deserialize<List<FinnhubSymbolItem>>(json, JsonOptions) ?? new List<FinnhubSymbolItem>();
                });

                return symbols ?? new List<FinnhubSymbolItem>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falling back to bundled symbols for cache key {CacheKey}", cacheKey);
                return fallbackSymbols?.ToList() ?? new List<FinnhubSymbolItem>();
            }
        }

        private static bool MatchesQuery(FinnhubSymbolItem item, string query)
        {
            return Contains(item.Symbol, query)
                || Contains(item.DisplaySymbol, query)
                || Contains(item.Description, query);
        }

        private static bool Contains(string? value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetMatchRank(FinnhubSymbolItem item, string query)
        {
            if (EqualsIgnoreCase(item.Symbol, query) || EqualsIgnoreCase(item.DisplaySymbol, query))
                return 0;

            if (StartsWith(item.Symbol, query) || StartsWith(item.DisplaySymbol, query))
                return 1;

            if (Contains(item.Symbol, query) || Contains(item.DisplaySymbol, query))
                return 2;

            if (StartsWith(item.Description, query))
                return 3;

            return 4;
        }

        private static bool EqualsIgnoreCase(string? value, string query)
        {
            return string.Equals(value, query, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWith(string? value, string query)
        {
            return !string.IsNullOrWhiteSpace(value)
                && value.StartsWith(query, StringComparison.OrdinalIgnoreCase);
        }

        private static readonly HashSet<string> MajorForexCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "NZD"
        };

        private static readonly string[] CryptoQueryHints =
        {
            "BTC", "ETH", "USDT", "USDC", "SOL", "BNB", "XRP", "DOGE", "ADA", "AVAX", "CRYPTO"
        };

        private static bool IsLikelyCryptoQuery(string query)
        {
            var q = query.Trim().ToUpperInvariant();
            if (CryptoQueryHints.Any(hint => q.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                return true;

            // Symbol patterns like BTCUSDT or ETHUSD are usually crypto queries.
            return q.Length is >= 6 and <= 12 && (q.EndsWith("USDT") || q.EndsWith("USDC") || q.EndsWith("BTC") || q.EndsWith("ETH"));
        }

        private static bool IsLikelyForexQuery(string query)
        {
            var q = query.Trim().ToUpperInvariant();

            if (q.Contains("/") || q.Contains("_"))
            {
                var parts = q.Replace("/", "_").Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                return parts.Length == 2 && MajorForexCodes.Contains(parts[0]) && MajorForexCodes.Contains(parts[1]);
            }

            if (q.Length == 6)
            {
                var baseCode = q[..3];
                var quoteCode = q[3..6];
                return MajorForexCodes.Contains(baseCode) && MajorForexCodes.Contains(quoteCode);
            }

            return false;
        }

        private static int GetAssetClassPriority(string assetClass, string query)
        {
            var cls = assetClass.Trim().ToLowerInvariant();
            var prefersCrypto = IsLikelyCryptoQuery(query);
            var prefersForex = IsLikelyForexQuery(query);

            if (prefersCrypto)
            {
                return cls == "crypto" ? 0 : cls == "forex" ? 1 : 2;
            }

            if (prefersForex)
            {
                return cls == "forex" ? 0 : cls == "crypto" ? 1 : 2;
            }

            // Neutral search: keep stock first, then crypto, then forex.
            return cls switch
            {
                "stock" => 0,
                "common stock" => 0,
                "etf" => 0,
                "etp" => 0,
                "crypto" => 1,
                "forex" => 2,
                _ => 3,
            };
        }

        private static bool TryParseForexPair(string symbol, out string baseCode, out string quoteCode)
        {
            baseCode = string.Empty;
            quoteCode = string.Empty;

            if (string.IsNullOrWhiteSpace(symbol))
                return false;

            var part = symbol.Trim();
            var colonIdx = part.IndexOf(':');
            if (colonIdx >= 0 && colonIdx < part.Length - 1)
            {
                part = part[(colonIdx + 1)..];
            }

            var normalized = part.Replace("/", "_").ToUpperInvariant();
            var split = normalized.Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (split.Length == 2 && split[0].Length == 3 && split[1].Length == 3)
            {
                baseCode = split[0];
                quoteCode = split[1];
                return true;
            }

            if (normalized.Length == 6)
            {
                baseCode = normalized[..3];
                quoteCode = normalized[3..6];
                return true;
            }

            return false;
        }

        private sealed class FrankfurterResponse
        {
            public string? Base { get; set; }
            public string? Date { get; set; }
            public Dictionary<string, decimal>? Rates { get; set; }
        }

        private sealed class FrankfurterTimeseriesResponse
        {
            public string? Base { get; set; }
            public Dictionary<string, Dictionary<string, decimal>>? Rates { get; set; }
        }

        private static bool TryExtractBinanceSymbol(string symbol, out string pair)
        {
            pair = string.Empty;
            if (!symbol.StartsWith("BINANCE:", StringComparison.OrdinalIgnoreCase))
                return false;

            var raw = symbol.Split(':', 2)[1].Trim().ToUpperInvariant();
            pair = raw.Replace("/", "").Replace("_", "");
            return pair.Length >= 6;
        }

        private static string ResolutionToBinanceInterval(string resolution)
        {
            var r = resolution.Trim().ToUpperInvariant();
            return r switch
            {
                "1" => "1m",
                "5" => "5m",
                "15" => "15m",
                "30" => "30m",
                "60" => "1h",
                "120" => "2h",
                "240" => "4h",
                "D" => "1d",
                _ => "5m",
            };
        }

        private async Task<string?> BuildBinanceCandlesFallbackAsync(string symbol, string resolution)
        {
            if (!TryExtractBinanceSymbol(symbol, out var pair))
                return null;

            var interval = ResolutionToBinanceInterval(resolution);
            var url = $"https://api.binance.com/api/v3/klines?symbol={pair}&interval={interval}&limit=300";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return null;

            var c = new List<decimal>();
            var h = new List<decimal>();
            var l = new List<decimal>();
            var o = new List<decimal>();
            var t = new List<long>();
            var v = new List<decimal>();

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 6)
                    continue;

                var openTimeMs = item[0].GetInt64();
                var open = decimal.Parse(item[1].GetString() ?? "0", CultureInfo.InvariantCulture);
                var high = decimal.Parse(item[2].GetString() ?? "0", CultureInfo.InvariantCulture);
                var low = decimal.Parse(item[3].GetString() ?? "0", CultureInfo.InvariantCulture);
                var close = decimal.Parse(item[4].GetString() ?? "0", CultureInfo.InvariantCulture);
                var vol = decimal.Parse(item[5].GetString() ?? "0", CultureInfo.InvariantCulture);

                t.Add(openTimeMs / 1000);
                o.Add(open);
                h.Add(high);
                l.Add(low);
                c.Add(close);
                v.Add(vol);
            }

            if (c.Count == 0)
                return null;

            return JsonSerializer.Serialize(new { c, h, l, o, s = "ok", t, v });
        }

        private static string NormalizeYahooSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return symbol;

            var trimmed = symbol.Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex >= 0 && colonIndex < trimmed.Length - 1)
            {
                trimmed = trimmed[(colonIndex + 1)..];
            }

            return trimmed.ToUpperInvariant();
        }

        private static string ResolutionToYahooInterval(string resolution)
        {
            var r = resolution.Trim().ToUpperInvariant();
            return r switch
            {
                "1" => "1m",
                "5" => "5m",
                "15" => "15m",
                "30" => "30m",
                "60" => "1h",
                "120" => "1h",
                "240" => "1h",
                "D" => "1d",
                _ => "5m",
            };
        }

        private async Task<string?> BuildYahooStockCandlesFallbackAsync(string symbol, string resolution, long fromUnix, long toUnix)
        {
            if (symbol.StartsWith("OANDA:", StringComparison.OrdinalIgnoreCase)
                || symbol.StartsWith("FXCM:", StringComparison.OrdinalIgnoreCase)
                || symbol.StartsWith("BINANCE:", StringComparison.OrdinalIgnoreCase)
                || symbol.StartsWith("COINBASE:", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var yahooSymbol = NormalizeYahooSymbol(symbol);
            var interval = ResolutionToYahooInterval(resolution);

            var url = QueryHelpers.AddQueryString(
                $"https://query1.finance.yahoo.com/v8/finance/chart/{yahooSymbol}",
                new Dictionary<string, string?>
                {
                    ["interval"] = interval,
                    ["period1"] = fromUnix.ToString(CultureInfo.InvariantCulture),
                    ["period2"] = toUnix.ToString(CultureInfo.InvariantCulture),
                    ["includePrePost"] = "false",
                    ["events"] = "div,splits"
                });

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json,text/plain,*/*");
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("chart", out var chartElement)
                || !chartElement.TryGetProperty("result", out var resultElement)
                || resultElement.ValueKind != JsonValueKind.Array
                || resultElement.GetArrayLength() == 0)
            {
                return null;
            }

            var result = resultElement[0];
            if (!result.TryGetProperty("timestamp", out var timestampElement)
                || timestampElement.ValueKind != JsonValueKind.Array
                || !result.TryGetProperty("indicators", out var indicatorsElement)
                || !indicatorsElement.TryGetProperty("quote", out var quoteElement)
                || quoteElement.ValueKind != JsonValueKind.Array
                || quoteElement.GetArrayLength() == 0)
            {
                return null;
            }

            var quote = quoteElement[0];
            if (!quote.TryGetProperty("close", out var closeElement) || closeElement.ValueKind != JsonValueKind.Array)
                return null;

            var openElement = quote.TryGetProperty("open", out var openProp) ? openProp : default;
            var highElement = quote.TryGetProperty("high", out var highProp) ? highProp : default;
            var lowElement = quote.TryGetProperty("low", out var lowProp) ? lowProp : default;
            var volumeElement = quote.TryGetProperty("volume", out var volumeProp) ? volumeProp : default;

            var c = new List<decimal>();
            var h = new List<decimal>();
            var l = new List<decimal>();
            var o = new List<decimal>();
            var t = new List<long>();
            var v = new List<decimal>();

            var length = Math.Min(timestampElement.GetArrayLength(), closeElement.GetArrayLength());

            for (var i = 0; i < length; i++)
            {
                var closePoint = closeElement[i];
                if (closePoint.ValueKind != JsonValueKind.Number)
                    continue;

                var close = closePoint.GetDecimal();
                if (close <= 0)
                    continue;

                var timestamp = timestampElement[i].ValueKind == JsonValueKind.Number
                    ? timestampElement[i].GetInt64()
                    : 0L;

                decimal ReadSeriesValue(JsonElement series, decimal fallback)
                {
                    if (series.ValueKind != JsonValueKind.Array || i >= series.GetArrayLength())
                        return fallback;

                    var value = series[i];
                    return value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : fallback;
                }

                t.Add(timestamp);
                c.Add(close);
                o.Add(ReadSeriesValue(openElement, close));
                h.Add(ReadSeriesValue(highElement, close));
                l.Add(ReadSeriesValue(lowElement, close));
                v.Add(ReadSeriesValue(volumeElement, 0));
            }

            if (c.Count == 0)
                return null;

            return JsonSerializer.Serialize(new { c, h, l, o, s = "ok", t, v });
        }

        private async Task<string?> BuildExternalForexCandlesFallbackAsync(string symbol)
        {
            if (!TryParseForexPair(symbol, out var baseCode, out var quoteCode))
                return null;

            var start = DateTime.UtcNow.Date.AddDays(-30).ToString("yyyy-MM-dd");
            var end = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var url = $"https://api.frankfurter.app/{start}..{end}?from={baseCode}&to={quoteCode}";

            using var client = new HttpClient();
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<FrankfurterTimeseriesResponse>(json, JsonOptions);
            if (data?.Rates is null || data.Rates.Count == 0)
                return null;

            var ordered = data.Rates
                .Where(kvp => kvp.Value.TryGetValue(quoteCode, out var rate) && rate > 0)
                .Select(kvp => new { Date = kvp.Key, Rate = kvp.Value[quoteCode] })
                .OrderBy(x => x.Date)
                .ToList();

            if (ordered.Count == 0)
                return null;

            var c = new List<decimal>();
            var h = new List<decimal>();
            var l = new List<decimal>();
            var o = new List<decimal>();
            var t = new List<long>();
            var v = new List<decimal>();

            foreach (var row in ordered)
            {
                var ts = DateTimeOffset.Parse(row.Date, CultureInfo.InvariantCulture).ToUnixTimeSeconds();
                t.Add(ts);
                o.Add(row.Rate);
                h.Add(row.Rate);
                l.Add(row.Rate);
                c.Add(row.Rate);
                v.Add(0);
            }

            return JsonSerializer.Serialize(new { c, h, l, o, s = "ok", t, v });
        }

        private async Task<string?> BuildExternalForexFallbackQuoteAsync(string symbol)
        {
            if (!TryParseForexPair(symbol, out var baseCode, out var quoteCode))
                return null;

            var latestUrl = $"https://api.frankfurter.app/latest?from={baseCode}&to={quoteCode}";
            using var latestClient = new HttpClient();
            var latestResponse = await latestClient.GetAsync(latestUrl);
            if (!latestResponse.IsSuccessStatusCode)
                return null;

            var latestJson = await latestResponse.Content.ReadAsStringAsync();
            var latest = JsonSerializer.Deserialize<FrankfurterResponse>(latestJson, JsonOptions);
            if (latest?.Rates is null || !latest.Rates.TryGetValue(quoteCode, out var close) || close <= 0)
                return null;

            // Attempt previous close for change fields.
            var previousClose = close;
            var priorDate = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
            try
            {
                var priorUrl = $"https://api.frankfurter.app/{priorDate}?from={baseCode}&to={quoteCode}";
                using var priorClient = new HttpClient();
                var priorResponse = await priorClient.GetAsync(priorUrl);
                if (priorResponse.IsSuccessStatusCode)
                {
                    var priorJson = await priorResponse.Content.ReadAsStringAsync();
                    var prior = JsonSerializer.Deserialize<FrankfurterResponse>(priorJson, JsonOptions);
                    if (prior?.Rates is not null && prior.Rates.TryGetValue(quoteCode, out var priorRate) && priorRate > 0)
                    {
                        previousClose = priorRate;
                    }
                }
            }
            catch
            {
                // Keep previousClose = latest when prior fetch is unavailable.
            }

            var delta = close - previousClose;
            decimal? deltaPercent = previousClose > 0 ? (delta / previousClose) * 100 : null;
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return JsonSerializer.Serialize(new
            {
                c = close,
                d = delta,
                dp = deltaPercent,
                h = close,
                l = close,
                o = previousClose,
                pc = previousClose,
                t = nowUnix
            });
        }

        private static bool HasUsableQuote(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                if (!doc.RootElement.TryGetProperty("c", out var closeProp))
                    return false;

                return closeProp.ValueKind == JsonValueKind.Number && closeProp.GetDecimal() > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasUsableCandles(string content)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("s", out var stateProp)
                    && stateProp.ValueKind == JsonValueKind.String
                    && string.Equals(stateProp.GetString(), "ok", StringComparison.OrdinalIgnoreCase)
                    && root.TryGetProperty("c", out var closeProp)
                    && closeProp.ValueKind == JsonValueKind.Array)
                {
                    return closeProp.GetArrayLength() > 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private async Task<string?> BuildCandleFallbackQuoteAsync(string symbol)
        {
            string? candleUrl = null;

            if (symbol.StartsWith("OANDA:", StringComparison.OrdinalIgnoreCase)
                || symbol.StartsWith("FXCM:", StringComparison.OrdinalIgnoreCase))
            {
                candleUrl = "https://finnhub.io/api/v1/forex/candle";
            }
            else if (symbol.StartsWith("BINANCE:", StringComparison.OrdinalIgnoreCase)
                     || symbol.StartsWith("COINBASE:", StringComparison.OrdinalIgnoreCase))
            {
                candleUrl = "https://finnhub.io/api/v1/crypto/candle";
            }

            if (candleUrl is null)
                return null;

            var to = DateTimeOffset.UtcNow;
            var from = to.AddDays(-2);

            var json = await FetchFinnhubContent(
                candleUrl,
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["resolution"] = "1",
                    ["from"] = from.ToUnixTimeSeconds().ToString(),
                    ["to"] = to.ToUnixTimeSeconds().ToString()
                });

            var candle = JsonSerializer.Deserialize<FinnhubCandleResponse>(json, JsonOptions);
            if (candle?.C is null || candle.C.Count == 0 || candle.S != "ok")
                return null;

            var lastIndex = candle.C.Count - 1;
            var close = candle.C[lastIndex];
            var open = candle.O is not null && candle.O.Count > lastIndex ? candle.O[lastIndex] : close;
            var high = candle.H is not null && candle.H.Count > lastIndex ? candle.H[lastIndex] : close;
            var low = candle.L is not null && candle.L.Count > lastIndex ? candle.L[lastIndex] : close;
            var timestamp = candle.T is not null && candle.T.Count > lastIndex ? candle.T[lastIndex] : to.ToUnixTimeSeconds();
            var previousClose = candle.C.Count > 1 ? candle.C[lastIndex - 1] : open;
            var delta = close - previousClose;
            decimal? deltaPercent = previousClose > 0 ? (delta / previousClose) * 100 : null;

            return JsonSerializer.Serialize(new
            {
                c = close,
                d = delta,
                dp = deltaPercent,
                h = high,
                l = low,
                o = open,
                pc = previousClose,
                t = timestamp
            });
        }

        private sealed class FinnhubSearchResponse
        {
            public List<FinnhubSymbolItem>? Result { get; set; }
        }

        private sealed class FinnhubCandleResponse
        {
            public List<decimal>? C { get; set; }
            public List<decimal>? H { get; set; }
            public List<decimal>? L { get; set; }
            public List<decimal>? O { get; set; }
            public string? S { get; set; }
            public List<long>? T { get; set; }
        }

        private sealed class FinnhubSymbolItem
        {
            public string? Description { get; set; }
            public string? DisplaySymbol { get; set; }
            public string? Symbol { get; set; }
            public string? Type { get; set; }
            public string? Currency { get; set; }
        }

        private sealed class MarketInstrumentSearchItem
        {
            public string Symbol { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string? DisplaySymbol { get; set; }
            public string AssetClass { get; set; } = string.Empty;
            public string? Exchange { get; set; }
            public string? Currency { get; set; }
            public int Rank { get; set; }
        }
        #endregion

        #region Endpoints

        [HttpGet("SymbolLookUp")]
        public async Task<IActionResult> SymbolLookUp(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/search",
                new Dictionary<string, string?>
                {
                    ["q"] = symbol
                });
        }

        [HttpGet("ForexSymbols")]
        public async Task<IActionResult> ForexSymbols(string exchange = "OANDA")
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/forex/symbol",
                new Dictionary<string, string?>
                {
                    ["exchange"] = exchange
                });
        }

        [HttpGet("CryptoSymbols")]
        public async Task<IActionResult> CryptoSymbols(string exchange = "BINANCE")
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/crypto/symbol",
                new Dictionary<string, string?>
                {
                    ["exchange"] = exchange
                });
        }

        [HttpGet("SearchMarketInstruments")]
        public async Task<IActionResult> SearchMarketInstruments(string query, int limit = 20)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Ok(new { count = 0, result = Array.Empty<object>() });
            }

            var normalizedQuery = query.Trim();
            var safeLimit = Math.Clamp(limit, 1, 50);

            try
            {
                var stockTask = FetchFinnhubContent(
                    "https://finnhub.io/api/v1/search",
                    new Dictionary<string, string?>
                    {
                        ["q"] = normalizedQuery
                    });

                var forexOandaTask = GetCachedSymbolsAsync(
                    "finnhub:forex:OANDA",
                    "https://finnhub.io/api/v1/forex/symbol",
                    new Dictionary<string, string?>
                    {
                        ["exchange"] = "OANDA"
                    },
                    DefaultForexSymbols);

                var cryptoBinanceTask = GetCachedSymbolsAsync(
                    "finnhub:crypto:BINANCE",
                    "https://finnhub.io/api/v1/crypto/symbol",
                    new Dictionary<string, string?>
                    {
                        ["exchange"] = "BINANCE"
                    },
                    DefaultCryptoSymbols);

                await Task.WhenAll(stockTask, forexOandaTask, cryptoBinanceTask);

                var stockPayload = JsonSerializer.Deserialize<FinnhubSearchResponse>(stockTask.Result, JsonOptions);
                var results = new List<MarketInstrumentSearchItem>();

                foreach (var item in stockPayload?.Result ?? new List<FinnhubSymbolItem>())
                {
                    var symbol = item.Symbol?.Trim();
                    if (string.IsNullOrWhiteSpace(symbol))
                        continue;

                    results.Add(new MarketInstrumentSearchItem
                    {
                        Symbol = symbol,
                        Description = item.Description ?? item.DisplaySymbol ?? symbol,
                        DisplaySymbol = item.DisplaySymbol,
                        AssetClass = string.IsNullOrWhiteSpace(item.Type) ? "stock" : item.Type!,
                        Currency = item.Currency,
                        Rank = GetMatchRank(item, normalizedQuery)
                    });
                }

                void AddMatches(IEnumerable<FinnhubSymbolItem> items, string assetClass, string exchange)
                {
                    foreach (var item in items)
                    {
                        var symbol = item.Symbol?.Trim();
                        if (string.IsNullOrWhiteSpace(symbol) || !MatchesQuery(item, normalizedQuery))
                            continue;

                        results.Add(new MarketInstrumentSearchItem
                        {
                            Symbol = symbol,
                            Description = item.Description ?? item.DisplaySymbol ?? symbol,
                            DisplaySymbol = item.DisplaySymbol,
                            AssetClass = assetClass,
                            Exchange = exchange,
                            Currency = item.Currency,
                            Rank = GetMatchRank(item, normalizedQuery)
                        });
                    }
                }

                AddMatches(forexOandaTask.Result, "forex", "OANDA");
                AddMatches(cryptoBinanceTask.Result, "crypto", "BINANCE");

                var merged = results
                    .GroupBy(item => item.Symbol, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderBy(item => GetAssetClassPriority(item.AssetClass, normalizedQuery))
                        .ThenBy(item => item.Rank)
                        .ThenBy(item => item.Symbol)
                        .First())
                    .OrderBy(item => GetAssetClassPriority(item.AssetClass, normalizedQuery))
                    .ThenBy(item => item.Rank)
                    .ThenBy(item => item.Symbol)
                    .Take(safeLimit)
                    .ToList();

                return Ok(new { count = merged.Count, result = merged });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Instrument search failed for query {Query}", query);
                return StatusCode(500, "Instrument search failed");
            }
        }

        [HttpGet("HistoricalCandles")]
        public async Task<IActionResult> HistoricalCandles(
            string symbol,
            string resolution = "5",
            long? from = null,
            long? to = null)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                return BadRequest("symbol is required");
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fromUnix = from ?? now - (24 * 60 * 60);
            var toUnix = to ?? now;

            var baseUrl = "https://finnhub.io/api/v1/stock/candle";
            if (symbol.StartsWith("OANDA:", StringComparison.OrdinalIgnoreCase)
                || symbol.StartsWith("FXCM:", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://finnhub.io/api/v1/forex/candle";
            }
            else if (symbol.StartsWith("BINANCE:", StringComparison.OrdinalIgnoreCase)
                     || symbol.StartsWith("COINBASE:", StringComparison.OrdinalIgnoreCase))
            {
                baseUrl = "https://finnhub.io/api/v1/crypto/candle";
            }

            try
            {
                var content = await FetchFinnhubContent(
                    baseUrl,
                    new Dictionary<string, string?>
                    {
                        ["symbol"] = symbol,
                        ["resolution"] = resolution,
                        ["from"] = fromUnix.ToString(),
                        ["to"] = toUnix.ToString()
                    });

                if (HasUsableCandles(content))
                    return Content(content, "application/json");

                _logger.LogWarning("Finnhub returned no usable candles for symbol {Symbol} at resolution {Resolution}", symbol, resolution);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Finnhub candles failed for symbol {Symbol}", symbol);
            }

            var stockFallback = await BuildYahooStockCandlesFallbackAsync(symbol, resolution, fromUnix, toUnix);
            if (!string.IsNullOrWhiteSpace(stockFallback))
                return Content(stockFallback, "application/json");

            var cryptoFallback = await BuildBinanceCandlesFallbackAsync(symbol, resolution);
            if (!string.IsNullOrWhiteSpace(cryptoFallback))
                return Content(cryptoFallback, "application/json");

            var forexFallback = await BuildExternalForexCandlesFallbackAsync(symbol);
            if (!string.IsNullOrWhiteSpace(forexFallback))
                return Content(forexFallback, "application/json");

            // Return a Finnhub-shaped no_data payload rather than 500 so
            // the frontend can gracefully fallback to live in-memory points.
            return Ok(new
            {
                c = Array.Empty<decimal>(),
                h = Array.Empty<decimal>(),
                l = Array.Empty<decimal>(),
                o = Array.Empty<decimal>(),
                s = "no_data",
                t = Array.Empty<long>(),
                v = Array.Empty<decimal>()
            });
        }

        [HttpGet("StockSymbols")]
        public async Task<IActionResult> StockSymbols(string exchange)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/symbol",
                new Dictionary<string, string?>
                {
                    ["exchange"] = exchange
                });
        }

        [HttpGet("MarketStatus")]
        public async Task<IActionResult> MarketStatus(string exchange)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/market-status",
                new Dictionary<string, string?>
                {
                    ["exchange"] = exchange
                });
        }

        [HttpGet("MarketHoliday")]
        public async Task<IActionResult> MarketHoliday(string exchange)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/market-holiday",
                new Dictionary<string, string?>
                {
                    ["exchange"] = exchange
                });
        }

        [HttpGet("MarketNews")]
        public async Task<IActionResult> MarketNews(string category = "general")
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/news",
                new Dictionary<string, string?>
                {
                    ["category"] = category
                });
        }

        [HttpGet("CompanyNews")]
        public async Task<IActionResult> CompanyNews(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/company-news",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("CompanyProfile")]
        public async Task<IActionResult> CompanyProfile(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/profile2",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
        }

        [HttpGet("CompanyPeers")]
        public async Task<IActionResult> CompanyPeers(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/peers",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
        }

        [HttpGet("BasicFinancials")]
        public async Task<IActionResult> BasicFinancials(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/metric",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["metric"] = "all"
                });
        }

        [HttpGet("InsiderTransactions")]
        public async Task<IActionResult> InsiderTransactions(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/insider-transactions",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
        }

        [HttpGet("InsiderSentiment")]
        public async Task<IActionResult> InsiderSentiment(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/insider-sentiment",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("FinancialsReported")]
        public async Task<IActionResult> FinancialsReported(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/financials-reported",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
        }

        [HttpGet("Filings")]
        public async Task<IActionResult> Filings(string symbol)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/filings",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
        }

        [HttpGet("IpoCalendar")]
        public async Task<IActionResult> IpoCalendar(string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/calendar/ipo",
                new Dictionary<string, string?>
                {
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("CompanyEarnings")]
        public async Task<IActionResult> CompanyEarnings(string symbol, int limit = 10)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/earnings",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["limit"] = limit.ToString()
                });
        }

        [HttpGet("Quote")]
        public async Task<IActionResult> Quote(string symbol)
        {
            try
            {
                var content = await FetchFinnhubContent(
                    "https://finnhub.io/api/v1/quote",
                    new Dictionary<string, string?>
                    {
                        ["symbol"] = symbol
                    });

                if (HasUsableQuote(content))
                    return Content(content, "application/json");

                try
                {
                    var fallback = await BuildCandleFallbackQuoteAsync(symbol);
                    if (!string.IsNullOrWhiteSpace(fallback))
                        return Content(fallback, "application/json");
                }
                catch (Exception candleEx)
                {
                    _logger.LogWarning(candleEx, "Candle fallback failed for symbol {Symbol}", symbol);
                }

                var externalFxFallback = await BuildExternalForexFallbackQuoteAsync(symbol);
                if (!string.IsNullOrWhiteSpace(externalFxFallback))
                    return Content(externalFxFallback, "application/json");

                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                try
                {
                    try
                    {
                        var fallback = await BuildCandleFallbackQuoteAsync(symbol);
                        if (!string.IsNullOrWhiteSpace(fallback))
                            return Content(fallback, "application/json");
                    }
                    catch (Exception candleEx)
                    {
                        _logger.LogWarning(candleEx, "Candle fallback failed in catch path for symbol {Symbol}", symbol);
                    }

                    var externalFxFallback = await BuildExternalForexFallbackQuoteAsync(symbol);
                    if (!string.IsNullOrWhiteSpace(externalFxFallback))
                        return Content(externalFxFallback, "application/json");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogWarning(fallbackEx, "Quote fallback failed for symbol {Symbol}", symbol);
                }

                _logger.LogError(ex, "Finnhub quote failed for symbol {Symbol}", symbol);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("StockUsptoPatent")]
        public async Task<IActionResult> StockUsptoPatent(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/uspto-patent",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("StockVisaApplication")]
        public async Task<IActionResult> StockVisaApplication(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/visa-application",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("StockLobbying")]
        public async Task<IActionResult> StockLobbying(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/lobbying",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        [HttpGet("StockUsaSpending")]
        public async Task<IActionResult> StockUsaSpending(string symbol, string from, string to)
        {
            return await CallFinnhub(
                "https://finnhub.io/api/v1/stock/usa-spending",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol,
                    ["from"] = from,
                    ["to"] = to
                });
        }

        #endregion
    }
}
