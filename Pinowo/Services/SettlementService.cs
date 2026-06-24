using Microsoft.EntityFrameworkCore;
using Pinowo.Data;

namespace Pinowo.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly ApplicationDbContext _db;
        private readonly IChainPaymentService _chain;

        public SettlementService(ApplicationDbContext db, IChainPaymentService chain)
        {
            _db = db;
            _chain = chain;
        }

        public async Task<SettleResult> SettleShareAsync(int expenseId, int shareId, int actingUserId)
        {
            var share = await _db.ExpenseShares
                .Include(s => s.User)
                .Include(s => s.Expense).ThenInclude(e => e.PaidByUser)
                .FirstOrDefaultAsync(s => s.Id == shareId && s.ExpenseId == expenseId);

            if (share is null) return new SettleResult(SettleOutcome.NotFound, 0);

            var groupId = share.Expense.GroupId;

            var isMember = await _db.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == actingUserId);
            if (!isMember) return new SettleResult(SettleOutcome.Forbidden, groupId);

            // Only the debtor (share owner) or the creditor (expense payer) may settle.
            if (actingUserId != share.UserId && actingUserId != share.Expense.PaidByUserId)
                return new SettleResult(SettleOutcome.Forbidden, groupId);

            if (share.IsSettled) return new SettleResult(SettleOutcome.AlreadySettled, groupId);

            // Exact pending balance for this share, in USD-equivalent, from the ledger.
            var amountUsd = Math.Round(
                share.Expense.AmountInUsdAtEntry * (share.ShareAmount / share.Expense.Amount), 2);

            share.IsSettled = true;
            share.SettledAt = DateTime.UtcNow;

            // Move the matching amount on-chain when wallets are configured. A null
            // result (disabled / unconfigured / chain error) leaves it a ledger-only
            // settlement so the action never fails because of the network.
            var tx = await _chain.SendSettlementAsync(
                share.User.Email, share.Expense.PaidByUser.Email, amountUsd);
            if (tx is not null)
            {
                share.SettlementTxHash = tx.TxHash;
                share.SettlementTokenAmount = amountUsd;
            }

            await _db.SaveChangesAsync();

            return new SettleResult(SettleOutcome.Settled, groupId, tx?.TxHash, tx?.ExplorerUrl, amountUsd);
        }
    }
}
