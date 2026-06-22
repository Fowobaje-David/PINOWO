using Pinowo.Models.Enums;

namespace Pinowo.Services
{
    /// <summary>
    /// Abstraction over the CoinGecko (or any) rate source, with caching.
    /// Keeping this as an interface means BalanceCalculatorService can be
    /// unit-tested with a fake rate provider instead of hitting the real API.
    /// </summary>
    public interface IExchangeRateService
    {
        /// <summary>
        /// Returns the current USD rate for the given currency.
        /// Implementations should cache (see PRD Section 9 - CoinGecko rate limits)
        /// and only refetch if the last snapshot is older than ~5 minutes.
        /// </summary>
        Task<decimal> GetUsdRateAsync(CurrencyType currency);
    }
}
