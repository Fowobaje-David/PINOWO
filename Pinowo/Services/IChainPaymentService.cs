namespace Pinowo.Services
{
    /// <summary>Result of an on-chain settlement transfer.</summary>
    public record ChainTransferResult(string TxHash, string ExplorerUrl);

    /// <summary>
    /// Sends a real testnet transfer (Ethereum Sepolia) when a debt is settled,
    /// so the demo shows actual movement of value on a public block explorer.
    /// Implementations must no-op gracefully (return null) when the chain feature
    /// is disabled or the involved wallets aren't configured, so settlement always works.
    /// </summary>
    public interface IChainPaymentService
    {
        bool IsEnabled { get; }

        /// <summary>True if both parties have wallets configured and a debtor key is available.</summary>
        bool CanSettle(string? debtorEmail, string? creditorEmail);

        /// <summary>
        /// Sends the testnet transfer from debtor to creditor for the given
        /// USD-equivalent amount (moved as the mock stablecoin token when configured,
        /// else a symbolic ETH amount). Returns the tx hash + explorer URL, or null
        /// if it couldn't/shouldn't send.
        /// </summary>
        Task<ChainTransferResult?> SendSettlementAsync(string? debtorEmail, string? creditorEmail, decimal amountUsd);
    }
}
