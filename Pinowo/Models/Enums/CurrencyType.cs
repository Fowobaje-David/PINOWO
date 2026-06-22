namespace Pinowo.Models.Enums
{
    /// <summary>
    /// Supported units of account for this MVP.
    /// Scope note: these represent VALUE denominations, not custodied wallets.
    /// No real coins move on-chain - see PRD Section 3 (Non-Goals).
    /// </summary>
    public enum CurrencyType
    {
        BTC = 0,
        STABLECOIN = 1 // e.g. USDT/USDC equivalent, pegged ~1:1 to USD
    }
}
