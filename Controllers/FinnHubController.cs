using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [Produces("application/json")]
    //[Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    public class FinnHubController : ControllerBase
    {
        #region Members
        private readonly IConfiguration _config;
        private readonly ILogger<FinnHubController> _logger;
        #endregion
        #region Constructor
        public FinnHubController (IConfiguration IConfiguration, ILogger<FinnHubController> ILogger)
        {
            _config = IConfiguration;
            _logger = ILogger;
        }
        #endregion
        [HttpGet]
        [Route("SymbolLookUp")]
        public async Task<IActionResult> SymbolLookUp(string symbol, string? exchange)
        {
            try
            {
                string key = _config["FinnHub:ApiKey"];

                var queryParams = new Dictionary<string, string?>
                {
                    ["q"] = symbol,
                    ["token"] = key
                };

                if (!string.IsNullOrWhiteSpace(exchange))
                {
                    queryParams["exchange"] = exchange;
                }

                var url = QueryHelpers.AddQueryString(
                    "https://finnhub.io/api/v1/search",
                    queryParams
                );

                using var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Error calling Finnhub");

                var content = await response.Content.ReadAsStringAsync();
                return Content(content, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to lookup symbol");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
