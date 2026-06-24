using Microsoft.Extensions.Options;
using Nethereum.StandardTokenEIP20;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Pinowo.Models;

namespace Pinowo
{
    /// <summary>
    /// One-time deploy of the mock ERC-20 stablecoin used to demonstrate matching
    /// on-chain settlement amounts. Run with: <c>dotnet run -- deploytoken</c>.
    /// Deploys from the first configured wallet that has a private key (the deployer
    /// receives the full initial supply), then prints the contract address to set as
    /// Chain:StablecoinTokenAddress in user-secrets.
    /// </summary>
    public static class TokenDeployer
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<ChainOptions>>().Value;

            var deployer = opts.Wallets.Values.FirstOrDefault(w => !string.IsNullOrWhiteSpace(w.PrivateKey));
            if (deployer is null)
            {
                Console.WriteLine("No wallet with a PrivateKey configured (Chain:Wallets:...:PrivateKey). Aborting.");
                return;
            }

            var account = new Account(deployer.PrivateKey, opts.ChainId);
            var web3 = new Web3(account, opts.RpcUrl);

            var initialBaseUnits = Web3.Convert.ToWei(opts.TokenInitialSupply, opts.TokenDecimals);
            Console.WriteLine($"Deploying {opts.TokenName} ({opts.TokenSymbol}), supply {opts.TokenInitialSupply} " +
                              $"to deployer {deployer.Address} on chain {opts.ChainId} ...");

            var deployment = new EIP20Deployment
            {
                InitialAmount = initialBaseUnits,
                TokenName = opts.TokenName,
                DecimalUnits = (byte)opts.TokenDecimals,
                TokenSymbol = opts.TokenSymbol
            };

            var service = await StandardTokenService.DeployContractAndGetServiceAsync(web3, deployment);
            var address = service.ContractHandler.ContractAddress;
            var balance = await service.BalanceOfQueryAsync(deployer.Address);

            Console.WriteLine();
            Console.WriteLine($"Token deployed at: {address}");
            Console.WriteLine($"Deployer balance : {Web3.Convert.FromWei(balance, opts.TokenDecimals)} {opts.TokenSymbol}");
            Console.WriteLine($"Explorer         : https://sepolia.etherscan.io/token/{address}");
            Console.WriteLine();
            Console.WriteLine("Now set it (keeps it out of the repo):");
            Console.WriteLine($"  dotnet user-secrets set \"Chain:StablecoinTokenAddress\" \"{address}\"");
        }
    }
}
