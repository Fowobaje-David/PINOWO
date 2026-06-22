using Microsoft.EntityFrameworkCore;
using Pinowo.Data;

namespace Pinowo.Services
{
    public class SettlementService : ISettlementService
    {
        private readonly ApplicationDbContext _db;

        public SettlementService(ApplicationDbContext db) => _db = db;

        public async Task<SettleResult> SettleShareAsync(int expenseId, int shareId, int actingUserId)
        {
            var share = await _db.ExpenseShares
                .Include(s => s.Expense)
                .FirstOrDefaultAsync(s => s.Id == shareId && s.ExpenseId == expenseId);

            if (share is null) return new SettleResult(SettleOutcome.NotFound, 0);

            var groupId = share.Expense.GroupId;

            var isMember = await _db.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == actingUserId);
            if (!isMember) return new SettleResult(SettleOutcome.Forbidden, groupId);

            // Only the debtor (share owner) or the creditor (expense payer) may
            // mark this share settled - not an unrelated group member.
            if (actingUserId != share.UserId && actingUserId != share.Expense.PaidByUserId)
                return new SettleResult(SettleOutcome.Forbidden, groupId);

            if (share.IsSettled) return new SettleResult(SettleOutcome.AlreadySettled, groupId);

            share.IsSettled = true;
            share.SettledAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return new SettleResult(SettleOutcome.Settled, groupId);
        }
    }
}
