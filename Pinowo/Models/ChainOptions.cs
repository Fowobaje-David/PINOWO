namespace Pinowo.Models
{
    /// <summary>
    /// Testnet (Ethereum Sepolia) settlement configuration. Bound from the
    /// "Chain" config section. Private keys must live in user-secrets / env,
    /// NEVER in committed appsettings - they are valueless testnet keys but we
    /// keep the same hygiene as real ones.
    /// </summary>
    public class ChainOptions
    {
        public const string SectionName = "Chain";

        /// <summary>Master switch. When false, settlement stays DB-only (no on-chain transfer).</summary>
        public bool Enabled { get; set; }

        public string RpcUrl { get; set; } = "https://ethereum-sepolia-rpc.publicnode.com";
        public long ChainId { get; set; } = 11155111; // Sepolia
        public string ExplorerTxBaseUrl { get; set; } = "https://sepolia.etherscan.io/tx/";

        /// <summary>
        /// Fallback symbolic ETH amount, used only when no stablecoin token is
        /// configured. When StablecoinTokenAddress is set, settlements transfer the
        /// matching token amount instead (see below).
        /// </summary>
        public decimal DemoTransferEth { get; set; } = 0.0001m;

        /// <summary>
        /// Address of the mock ERC-20 stablecoin (deploy with `dotnet run -- deploytoken`).
        /// When set, a settlement transfers this token in the settled USD-equivalent
        /// amount (so the on-chain movement matches the app figure). Gas is still ETH.
        /// </summary>
        public string? StablecoinTokenAddress { get; set; }
        public int TokenDecimals { get; set; } = 18;
        public string TokenSymbol { get; set; } = "pUSD";
        public string TokenName { get; set; } = "Pínówó USD";
        /// <summary>Initial supply minted to the deployer wallet at deploy time.</summary>
        public decimal TokenInitialSupply { get; set; } = 1_000_000m;

        /// <summary>email -> wallet. Populate via user-secrets (keys) only.</summary>
        public Dictionary<string, ChainWallet> Wallets { get; set; }
            = new(StringComparer.OrdinalIgnoreCase);
    }

    public class ChainWallet
    {
        public string Address { get; set; } = string.Empty;
        public string PrivateKey { get; set; } = string.Empty;
    }
}
