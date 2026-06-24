using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.ViewModels;

namespace Pinowo.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly ChainOptions _chain;

        public TransactionsController(
            ApplicationDbContext db,
            UserManager<User> userManager,
            IOptions<ChainOptions> chain)
        {
            _db = db;
            _userManager = userManager;
            _chain = chain.Value;
        }

        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(_userManager.GetUserId(User)!);

            var items = await _db.ExpenseShares
                .Where(s => s.IsSettled && (s.UserId == userId || s.Expense.PaidByUserId == userId))
                .OrderByDescending(s => s.SettledAt)
                .Select(s => new TransactionHistoryItem
                {
                    SettledAt = s.SettledAt,
                    GroupName = s.Expense.Group.Name,
                    Description = s.Expense.Description,
                    FromName = s.User.Name,
                    ToName = s.Expense.PaidByUser.Name,
                    AmountUsd = s.SettlementTokenAmount
                        ?? Math.Round(s.Expense.AmountInUsdAtEntry * (s.ShareAmount / s.Expense.Amount), 2),
                    Currency = s.Expense.Currency,
                    TxHash = s.SettlementTxHash,
                    IsIncoming = s.Expense.PaidByUserId == userId
                })
                .ToListAsync();

            ViewBag.ExplorerBase = _chain.ExplorerTxBaseUrl;
            ViewBag.TokenSymbol = _chain.TokenSymbol;
            return View(items);
        }
    }
}
