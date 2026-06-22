using Pinowo.Services.Dtos;

namespace Pinowo.Services
{
    public interface IBalanceCalculatorService
    {
        /// <summary>
        /// Computes net pairwise and overall balances for a group, in USD-equivalent,
        /// and produces a minimal settlement suggestion list.
        /// </summary>
        Task<GroupBalanceResult> CalculateGroupBalancesAsync(int groupId);
    }
}
