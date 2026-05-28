using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Models.DTO;

namespace VoxTrade.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Produces("application/json")]
    public class CurrenciesController : ControllerBase
    {
        private readonly TradingDbContext _dbContext;

        public CurrenciesController(TradingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrencies()
        {
            var currencies = await _dbContext.Currencies
                .AsNoTracking()
                .Where(c => c.IsDeleted != true)
                .OrderBy(c => c.NameEn)
                .Select(c => new
                {
                    c.Id,
                    c.NameEn,
                    c.NameAr,
                    c.Symbol,
                    c.UsdRate,
                })
                .ToListAsync();

            return Ok(currencies);
        }

        [HttpPost("convert")]
        public async Task<IActionResult> Convert([FromBody] CurrencyConversionRequest request)
        {
            if (request.Amount < 0)
            {
                return BadRequest(new { success = false, message = "Amount must be non-negative." });
            }

            var fromSymbol = (request.FromCurrency ?? "USD").Trim().ToUpperInvariant();
            var toSymbol = (request.ToCurrency ?? "USD").Trim().ToUpperInvariant();

            var fromRate = await GetUsdRateAsync(fromSymbol);
            var toRate = await GetUsdRateAsync(toSymbol);

            if (fromRate == null || toRate == null)
            {
                return BadRequest(new { success = false, message = "Invalid currency symbol." });
            }

            var amountInUsd = fromSymbol == "USD" ? request.Amount : request.Amount / fromRate.Value;
            var convertedAmount = toSymbol == "USD" ? amountInUsd : amountInUsd * toRate.Value;

            return Ok(new
            {
                success = true,
                amount = request.Amount,
                fromCurrency = fromSymbol,
                toCurrency = toSymbol,
                convertedAmount,
                fromUsdRate = fromRate.Value,
                toUsdRate = toRate.Value,
            });
        }

        [HttpPost("calculate-order-batch")]
        public async Task<IActionResult> CalculateOrderBatch([FromBody] OrderCalculationBatchRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return BadRequest(new { success = false, message = "Items are required." });
            }

            var toSymbol = (request.ToCurrency ?? "USD").Trim().ToUpperInvariant();
            var toRate = await GetUsdRateAsync(toSymbol);

            if (toRate == null)
            {
                return BadRequest(new { success = false, message = "Invalid target currency." });
            }

            var sourceSymbols = request.Items
                .Select(item => (item.FromCurrency ?? "USD").Trim().ToUpperInvariant())
                .Distinct()
                .ToList();

            var dbRates = await _dbContext.Currencies
                .AsNoTracking()
                .Where(c => c.IsDeleted != true && sourceSymbols.Contains(c.Symbol.ToUpper()))
                .Select(c => new { Symbol = c.Symbol.ToUpper(), c.UsdRate })
                .ToListAsync();

            var rateMap = dbRates.ToDictionary(rate => rate.Symbol, rate => rate.UsdRate);
            rateMap["USD"] = 1m;

            var results = new List<object>();

            foreach (var item in request.Items)
            {
                var sourceSymbol = (item.FromCurrency ?? "USD").Trim().ToUpperInvariant();
                if (!rateMap.TryGetValue(sourceSymbol, out var sourceRate) || sourceRate <= 0)
                {
                    return BadRequest(new { success = false, message = $"Invalid source currency for item {item.OrderId}." });
                }

                var baseAmount = item.Quantity * item.UnitPrice;
                var amountInUsd = sourceSymbol == "USD" ? baseAmount : baseAmount / sourceRate;
                var convertedTotal = toSymbol == "USD" ? amountInUsd : amountInUsd * toRate.Value;

                results.Add(new
                {
                    item.OrderId,
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice,
                    sourceCurrency = sourceSymbol,
                    targetCurrency = toSymbol,
                    baseAmount,
                    convertedTotal,
                });
            }

            return Ok(new
            {
                success = true,
                toCurrency = toSymbol,
                toUsdRate = toRate.Value,
                items = results,
            });
        }

        private async Task<decimal?> GetUsdRateAsync(string symbol)
        {
            if (symbol == "USD") return 1m;

            var rate = await _dbContext.Currencies
                .AsNoTracking()
                .Where(c => c.IsDeleted != true && c.Symbol.ToUpper() == symbol)
                .Select(c => (decimal?)c.UsdRate)
                .FirstOrDefaultAsync();

            return rate;
        }
    }
}
