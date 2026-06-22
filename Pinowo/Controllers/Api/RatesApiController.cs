using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models.Enums;
using Pinowo.Services;

namespace Pinowo.Controllers.Api
{
    /// <summary>
    /// REST API for exchange rates (PRD Section 6):
    ///   GET /api/rates/current  → latest BTC & stablecoin USD rates (cached).
    /// Anonymous: rates are public reference data with no per-user content.
    /// </summary>
    [ApiController]
    [Route("api/rates")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class RatesApiController : ControllerBase
    {
        private readonly IExchangeRateService _rateService;
        private readonly ApplicationDbContext _db;

        public RatesApiController(IExchangeRateService rateService, ApplicationDbContext db)
        {
            _rateService = rateService;
            _db = db;
        }

        [HttpGet("current")]
        public async Task<IActionResult> Current()
        {
            var result = new List<RateDto>();
            foreach (var currency in new[] { CurrencyType.BTC, CurrencyType.STABLECOIN })
            {
                // Ensures a fresh-or-cached value (and a snapshot) exists.
                await _rateService.GetUsdRateAsync(currency);

                var snap = await _db.ExchangeRateSnapshots
                    .Where(s => s.Currency == currency)
                    .OrderByDescending(s => s.FetchedAt)
                    .FirstAsync();

                result.Add(new RateDto(currency, snap.UsdRate, snap.FetchedAt));
            }
            return Ok(result);
        }
    }
}
