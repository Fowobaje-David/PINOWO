using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Hubs;
using Pinowo.Models;
using Pinowo.Services;
using Pinowo.ViewModels;

namespace Pinowo.Controllers
{
    /// <summary>
    /// Settle Up screen (PRD Section 7 #5): shows the minimal suggested
    /// transactions to zero out debts and lets the user mark their own
    /// outstanding shares as settled.
    /// </summary>
    [Authorize]
    public class SettlementsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IBalanceCalculatorService _calculator;
        private readonly ISettlementService _settlement;
        private readonly IHubContext<BalancesHub> _hub;

        public SettlementsController(
            ApplicationDbContext db,
            UserManager<User> userManager,
            IBalanceCalculatorService calculator,
            ISettlementService settlement,
            IHubContext<BalancesHub> hub)
        {
            _db = db;
            _userManager = userManager;
            _calculator = calculator;
            _settlement = settlement;
            _hub = hub;
        }

        private int CurrentUserId => int.Parse(_userManager.GetUserId(User)!);

        // GET /Settlements/Index?groupId=5  (the "Settle Up" screen)
        [HttpGet]
        public async Task<IActionResult> Index(int groupId)
        {
            var userId = CurrentUserId;
            var group = await _db.Groups.FirstOrDefaultAsync(g => g.Id == groupId);
            if (group is null) return NotFound();
            if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
                return Forbid();

            var balances = await _calculator.CalculateGroupBalancesAsync(groupId);

            var myDebts = await _db.ExpenseShares
                .Include(s => s.Expense).ThenInclude(e => e.PaidByUser)
                .Where(s => s.Expense.GroupId == groupId
                            && s.UserId == userId
                            && s.Expense.PaidByUserId != userId
                            && !s.IsSettled)
                .OrderByDescending(s => s.Expense.CreatedAt)
                .Select(s => new OutstandingShareItem
                {
                    ExpenseId = s.ExpenseId,
                    ShareId = s.Id,
                    Description = s.Expense.Description,
                    CreditorName = s.Expense.PaidByUser.Name,
                    ShareAmount = s.ShareAmount,
                    Currency = s.Expense.Currency,
                    ApproxUsd = Math.Round(
                        s.Expense.AmountInUsdAtEntry * (s.ShareAmount / s.Expense.Amount), 2)
                })
                .ToListAsync();

            return View(new SettleUpViewModel
            {
                GroupId = groupId,
                GroupName = group.Name,
                SuggestedSettlements = balances.SuggestedSettlements,
                MyDebts = myDebts
            });
        }

        // POST /Settlements/Settle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settle(int expenseId, int shareId, int groupId)
        {
            var result = await _settlement.SettleShareAsync(expenseId, shareId, CurrentUserId);

            switch (result.Outcome)
            {
                case SettleOutcome.Settled:
                    await _hub.Clients.Group(BalancesHub.GroupName(result.GroupId))
                        .SendAsync("BalancesChanged", result.GroupId);

                    if (result.ExplorerUrl is not null)
                    {
                        TempData["StatusMessage"] = $"Settled {result.AmountUsd:0.00} pUSD on Sepolia.";
                        TempData["TxUrl"] = result.ExplorerUrl;
                    }
                    else
                    {
                        TempData["StatusMessage"] = "Marked as settled.";
                    }
                    break;
                case SettleOutcome.AlreadySettled:
                    TempData["StatusMessage"] = "That share was already settled.";
                    break;
                case SettleOutcome.NotFound:
                    return NotFound();
                case SettleOutcome.Forbidden:
                    return Forbid();
            }

            return RedirectToAction(nameof(Index), new { groupId });
        }
    }
}
