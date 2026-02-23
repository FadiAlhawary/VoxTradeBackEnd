using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

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
        #endregion

        #region Constructor
        public FinnHubController(IConfiguration configuration, ILogger<FinnHubController> logger)
        {
            _config = configuration;
            _logger = logger;
        }
        #endregion

        #region Helper Method
        private async Task<IActionResult> CallFinnhub(string baseUrl, Dictionary<string, string?> parameters)
        {
            try
            {
                string key = _config["FinnHub:ApiKey"];
                parameters["token"] = key;

                var url = QueryHelpers.AddQueryString(baseUrl, parameters);

                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Error calling Finnhub");

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Finnhub API call failed");
                return StatusCode(500, "Internal server error");
            }
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
            return await CallFinnhub(
                "https://finnhub.io/api/v1/quote",
                new Dictionary<string, string?>
                {
                    ["symbol"] = symbol
                });
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
