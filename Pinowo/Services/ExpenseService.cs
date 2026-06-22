using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.Models.Enums;

namespace Pinowo.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly ApplicationDbContext _db;
        private readonly IExchangeRateService _rateService;

        public ExpenseService(ApplicationDbContext db, IExchangeRateService rateService)
        {
            _db = db;
            _rateService = rateService;
        }

        public async Task<Expense> AddExpenseAsync(
            int groupId,
            int paidByUserId,
            string description,
            decimal amount,
            CurrencyType currency)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

            var memberIds = await _db.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Select(gm => gm.UserId)
                .ToListAsync();

            if (memberIds.Count == 0)
                throw new InvalidOperationException("Group has no members.");
            if (!memberIds.Contains(paidByUserId))
                throw new InvalidOperationException("Payer must be a member of the group.");

            // Snapshot the USD-equivalent value AT ENTRY TIME (PRD Section 5).
            var usdRate = await _rateService.GetUsdRateAsync(currency);
            var amountInUsdAtEntry = Math.Round(amount * usdRate, 2, MidpointRounding.AwayFromZero);

            var expense = new Expense
            {
                GroupId = groupId,
                PaidByUserId = paidByUserId,
                Description = description,
                Amount = amount,
                Currency = currency,
                AmountInUsdAtEntry = amountInUsdAtEntry,
                CreatedAt = DateTime.UtcNow
            };

            // Equal split across every member (payer included; their own share
            // nets out in the balance calculation).
            var splits = IExpenseService.EqualSplit(amount, memberIds.Count);
            for (var i = 0; i < memberIds.Count; i++)
            {
                expense.Shares.Add(new ExpenseShare
                {
                    UserId = memberIds[i],
                    ShareAmount = splits[i],
                    IsSettled = false
                });
            }

            _db.Expenses.Add(expense);
            await _db.SaveChangesAsync();
            return expense;
        }
    }
}
