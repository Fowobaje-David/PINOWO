using Pinowo.Models;
using Pinowo.Models.Enums;

namespace Pinowo.Services
{
    public interface IExpenseService
    {
        /// <summary>
        /// Creates an expense in a group, snapshots its USD-equivalent value at
        /// the current rate, and generates an equal-split ExpenseShare for every
        /// current member (including the payer - the payer's own share nets out
        /// in the balance calculation). Returns the persisted Expense with shares.
        /// </summary>
        Task<Expense> AddExpenseAsync(
            int groupId,
            int paidByUserId,
            string description,
            decimal amount,
            CurrencyType currency);

        /// <summary>
        /// Splits <paramref name="amount"/> into <paramref name="memberCount"/>
        /// shares whose sum is EXACTLY <paramref name="amount"/> (no lost/created
        /// satoshis). Any indivisible remainder is spread one 1e-8 unit at a time
        /// across the first shares. Pure/static so it is unit-testable in isolation.
        /// </summary>
        static List<decimal> EqualSplit(decimal amount, int memberCount)
        {
            if (memberCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(memberCount));

            const decimal unit = 0.00000001m; // 1e-8, the BTC satoshi precision

            // Truncate the per-head amount down to 8 decimal places.
            var perHead = Math.Truncate(amount / memberCount / unit) * unit;
            var shares = Enumerable.Repeat(perHead, memberCount).ToList();

            // Distribute the leftover units (amount - perHead*n) one at a time.
            var remainder = amount - perHead * memberCount;
            var extraUnits = (int)Math.Round(remainder / unit, MidpointRounding.AwayFromZero);
            for (var i = 0; i < extraUnits && i < memberCount; i++)
                shares[i] += unit;

            return shares;
        }
    }
}
