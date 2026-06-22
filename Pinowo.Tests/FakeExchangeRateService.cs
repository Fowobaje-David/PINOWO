using Pinowo.Models.Enums;
using Pinowo.Services;

namespace Pinowo.Tests
{
    /// <summary>
    /// Deterministic rate provider for tests. These rates are only echoed back
    /// in GroupBalanceResult.RatesUsedUsd - the balance MATH uses each expense's
    /// AmountInUsdAtEntry snapshot, so these numbers do not affect who-owes-whom.
    /// </summary>
    public class FakeExchangeRateService : IExchangeRateService
    {
        public Task<decimal> GetUsdRateAsync(CurrencyType currency) =>
            Task.FromResult(currency == CurrencyType.BTC ? 100_000m : 1.00m);
    }
}
