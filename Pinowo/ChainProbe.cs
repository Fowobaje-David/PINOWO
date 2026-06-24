using Nethereum.Web3;

namespace Pinowo
{
    /// <summary>
    /// Connectivity smoke test for the testnet integration. Run with:
    /// <c>dotnet run -- chainprobe</c>. Confirms Nethereum can reach the Sepolia
    /// RPC in-process and read chain state. Optionally reads an address balance
    /// when PINOWO_PROBE_ADDRESS is set. No transactions are sent.
    /// </summary>
    public static class ChainProbe
    {
        public static async Task RunAsync()
        {
            var rpc = Environment.GetEnvironmentVariable("PINOWO_RPC_URL")
                ?? "https://ethereum-sepolia-rpc.publicnode.com";

            Console.WriteLine($"Connecting to {rpc} ...");
            var web3 = new Web3(rpc);

            var chainId = await web3.Eth.ChainId.SendRequestAsync();
            var block = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            Console.WriteLine($"chainId     : {chainId.Value}  (Sepolia = 11155111)");
            Console.WriteLine($"blockNumber : {block.Value}");

            var addr = Environment.GetEnvironmentVariable("PINOWO_PROBE_ADDRESS");
            if (!string.IsNullOrWhiteSpace(addr))
            {
                var bal = await web3.Eth.GetBalance.SendRequestAsync(addr);
                Console.WriteLine($"balance     : {Web3.Convert.FromWei(bal.Value)} ETH ({addr})");
            }

            Console.WriteLine("Nethereum -> Sepolia connectivity OK.");
        }
    }
}
