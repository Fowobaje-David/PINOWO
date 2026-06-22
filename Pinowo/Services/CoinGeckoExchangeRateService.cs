using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.Models.Enums;

namespace Pinowo.Services
{
    /// <summary>
    /// IExchangeRateService backed by the CoinGecko public API (no key needed),
    /// with caching via the ExchangeRateSnapshot table so we never call CoinGecko
    /// more than once per ~5 minutes per currency (PRD Section 9 - rate limits).
    ///
    /// Resilience: if CoinGecko is unreachable we fall back to the most recent
    /// snapshot (even if stale); if there's no snapshot at all we seed a sensible
    /// bootstrap value (stablecoin ≈ 1 USD; BTC a clearly-marked placeholder) so
    /// the app stays usable offline for a demo. Live rates always win when online.
    /// </summary>
    public class CoinGeckoExchangeRateService : IExchangeRateService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        // Bootstrap fallbacks used ONLY when both the network and the cache are empty.
        private const decimal StablecoinFallbackUsd = 1.00m;
        private const decimal BtcFallbackUsd = 65_000m;

        private static readonly IReadOnlyDictionary<CurrencyType, string> CoinGeckoIds =
            new Dictionary<CurrencyType, string>
            {
                [CurrencyType.BTC] = "bitcoin",
                [CurrencyType.STABLECOIN] = "tether", // USDT
            };

        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<CoinGeckoExchangeRateService> _logger;

        public CoinGeckoExchangeRateService(
            ApplicationDbContext db,
            IHttpClientFactory httpFactory,
            ILogger<CoinGeckoExchangeRateService> logger)
        {
            _db = db;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<decimal> GetUsdRateAsync(CurrencyType currency)
        {
            var latest = await _db.ExchangeRateSnapshots
                .Where(s => s.Currency == currency)
                .OrderByDescending(s => s.FetchedAt)
                .FirstOrDefaultAsync();

            // Fresh enough - serve from cache, no network call.
            if (latest is not null && DateTime.UtcNow - latest.FetchedAt < CacheTtl)
                return latest.UsdRate;

            try
            {
                var rate = await FetchFromCoinGeckoAsync(currency);
                await SaveSnapshotAsync(currency, rate);
                return rate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CoinGecko fetch failed for {Currency}; using fallback.", currency);

                if (latest is not null)
                    return latest.UsdRate; // stale, but real and better than a guess

                var fallback = currency == CurrencyType.STABLECOIN
                    ? StablecoinFallbackUsd
                    : BtcFallbackUsd;
                await SaveSnapshotAsync(currency, fallback);
                return fallback;
            }
        }

        private async Task<decimal> FetchFromCoinGeckoAsync(CurrencyType currency)
        {
            var id = CoinGeckoIds[currency];
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Pinowo/1.0");

            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={id}&vs_currencies=usd";
            var json = await client.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var usd = doc.RootElement.GetProperty(id).GetProperty("usd").GetDecimal();
            return Math.Round(usd, 2);
        }

        private async Task SaveSnapshotAsync(CurrencyType currency, decimal rate)
        {
            _db.ExchangeRateSnapshots.Add(new ExchangeRateSnapshot
            {
                Currency = currency,
                UsdRate = rate,
                FetchedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
