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
    /// MVC flow for adding an expense to a group (PRD Section 7 screen #4).
    /// Equal-split is the only MVP split method; the actual split + USD snapshot
    /// live in <see cref="ExpenseService"/> so the UI and API share one code path.
    /// </summary>
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IExpenseService _expenseService;
        private readonly IHubContext<BalancesHub> _hub;

        public ExpensesController(
            ApplicationDbContext db,
            UserManager<User> userManager,
            IExpenseService expenseService,
            IHubContext<BalancesHub> hub)
        {
            _db = db;
            _userManager = userManager;
            _expenseService = expenseService;
            _hub = hub;
        }

        private int CurrentUserId => int.Parse(_userManager.GetUserId(User)!);

        // GET /Expenses/Add?groupId=5
        [HttpGet]
        public async Task<IActionResult> Add(int groupId)
        {
            var group = await LoadGroupIfMemberAsync(groupId);
            if (group is null) return Forbid();

            var vm = new AddExpenseViewModel
            {
                GroupId = group.Id,
                GroupName = group.Name,
                PaidByUserId = CurrentUserId,
                Members = group.Members
                    .OrderBy(m => m.User.Name)
                    .Select(m => (m.UserId, m.User.Name))
                    .ToList()
            };
            return View(vm);
        }

        // POST /Expenses/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(AddExpenseViewModel model)
        {
            var group = await LoadGroupIfMemberAsync(model.GroupId);
            if (group is null) return Forbid();

            // Repopulate dropdown data in case we fall back to the view.
            model.GroupName = group.Name;
            model.Members = group.Members
                .OrderBy(m => m.User.Name)
                .Select(m => (m.UserId, m.User.Name))
                .ToList();

            if (!group.Members.Any(m => m.UserId == model.PaidByUserId))
                ModelState.AddModelError(nameof(model.PaidByUserId), "Payer must be a group member.");

            if (!ModelState.IsValid) return View(model);

            await _expenseService.AddExpenseAsync(
                model.GroupId, model.PaidByUserId, model.Description, model.Amount, model.Currency);

            // Push to everyone watching this group so balances refresh live.
            await _hub.Clients.Group(BalancesHub.GroupName(model.GroupId))
                .SendAsync("BalancesChanged", model.GroupId);

            TempData["StatusMessage"] = "Expense added.";
            return RedirectToAction("Details", "Groups", new { id = model.GroupId });
        }

        private async Task<Group?> LoadGroupIfMemberAsync(int groupId)
        {
            var group = await _db.Groups
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == groupId);

            if (group is null || !group.Members.Any(m => m.UserId == CurrentUserId))
                return null;
            return group;
        }
    }
}
