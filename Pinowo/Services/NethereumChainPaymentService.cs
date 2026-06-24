using Microsoft.Extensions.Options;
using Nethereum.StandardTokenEIP20;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Pinowo.Models;

namespace Pinowo.Services
{
    /// <summary>
    /// Sepolia testnet implementation. Sends a small native test-ETH transfer
    /// from the debtor's wallet to the creditor's wallet using Nethereum, so the
    /// settlement produces a real, verifiable transaction on a public explorer.
    /// Wallet keys come from ChainOptions (user-secrets), never the database.
    /// </summary>
    public class NethereumChainPaymentService : IChainPaymentService
    {
        private readonly ChainOptions _opts;
        private readonly ILogger<NethereumChainPaymentService> _logger;

        public NethereumChainPaymentService(
            IOptions<ChainOptions> opts,
            ILogger<NethereumChainPaymentService> logger)
        {
            _opts = opts.Value;
            _logger = logger;
        }

        public bool IsEnabled => _opts.Enabled;

        public bool CanSettle(string? debtorEmail, string? creditorEmail)
        {
            if (!_opts.Enabled || debtorEmail is null || creditorEmail is null) return false;
            return _opts.Wallets.TryGetValue(debtorEmail, out var debtor)
                   && !string.IsNullOrWhiteSpace(debtor.PrivateKey)
                   && _opts.Wallets.TryGetValue(creditorEmail, out var creditor)
                   && !string.IsNullOrWhiteSpace(creditor.Address);
        }

        public async Task<ChainTransferResult?> SendSettlementAsync(
            string? debtorEmail, string? creditorEmail, decimal amountUsd)
        {
            if (!CanSettle(debtorEmail, creditorEmail)) return null;

            var debtor = _opts.Wallets[debtorEmail!];
            var creditor = _opts.Wallets[creditorEmail!];

            try
            {
                // chainId in the account enables EIP-155 replay protection.
                var account = new Account(debtor.PrivateKey, _opts.ChainId);
                var web3 = new Web3(account, _opts.RpcUrl);

                string txHash;
                if (!string.IsNullOrWhiteSpace(_opts.StablecoinTokenAddress))
                {
                    // Transfer the MATCHING amount of the mock stablecoin (USD-equivalent).
                    var token = new StandardTokenService(web3, _opts.StablecoinTokenAddress);
                    var value = Web3.Convert.ToWei(amountUsd, _opts.TokenDecimals);
                    txHash = await token.TransferRequestAsync(creditor.Address, value);
                    _logger.LogInformation("On-chain settlement {From} -> {To}: {Amount} {Sym} tx {Tx}",
                        debtorEmail, creditorEmail, amountUsd, _opts.TokenSymbol, txHash);
                }
                else
                {
                    // No token configured: fall back to a symbolic ETH transfer.
                    txHash = await web3.Eth.GetEtherTransferService()
                        .TransferEtherAsync(creditor.Address, _opts.DemoTransferEth);
                    _logger.LogInformation("On-chain settlement (ETH) {From} -> {To}: tx {Tx}",
                        debtorEmail, creditorEmail, txHash);
                }

                return new ChainTransferResult(txHash, _opts.ExplorerTxBaseUrl + txHash);
            }
            catch (Exception ex)
            {
                // Never let a chain hiccup break the (already-recorded) settlement.
                _logger.LogWarning(ex, "On-chain settlement transfer failed; settlement stays DB-only.");
                return null;
            }
        }
    }
}
