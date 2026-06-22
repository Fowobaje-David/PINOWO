using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Hubs;
using Pinowo.Models;
using Pinowo.Services;

namespace Pinowo.Controllers.Api
{
    /// <summary>
    /// REST API for expenses (PRD Section 6):
    ///   POST /api/groups/{groupId}/expenses
    ///   GET  /api/groups/{groupId}/expenses
    /// Authenticated via the Identity cookie; caller must be a group member.
    /// </summary>
    [ApiController]
    [Route("api/groups/{groupId:int}/expenses")]
    [Authorize]
    [Produces("application/json")]
    public class ExpensesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IExpenseService _expenseService;
        private readonly IHubContext<BalancesHub> _hub;

        public ExpensesApiController(
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

        private Task<bool> IsMemberAsync(int groupId) =>
            _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == CurrentUserId);

        [HttpPost]
        public async Task<IActionResult> Create(int groupId, [FromBody] CreateExpenseRequest req)
        {
            if (!await _db.Groups.AnyAsync(g => g.Id == groupId)) return NotFound();
            if (!await IsMemberAsync(groupId)) return Forbid();

            try
            {
                var expense = await _expenseService.AddExpenseAsync(
                    groupId, req.PaidByUserId, req.Description, req.Amount, req.Currency);

                // Push to everyone watching this group so balances refresh live.
                await _hub.Clients.Group(BalancesHub.GroupName(groupId))
                    .SendAsync("BalancesChanged", groupId);

                return CreatedAtAction(nameof(GetOne),
                    new { groupId, expenseId = expense.Id }, await BuildDtoAsync(expense.Id));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> List(int groupId)
        {
            if (!await _db.Groups.AnyAsync(g => g.Id == groupId)) return NotFound();
            if (!await IsMemberAsync(groupId)) return Forbid();

            var ids = await _db.Expenses
                .Where(e => e.GroupId == groupId)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => e.Id)
                .ToListAsync();

            var dtos = new List<ExpenseDto>();
            foreach (var id in ids) dtos.Add(await BuildDtoAsync(id));
            return Ok(dtos);
        }

        // Not in Section 6 but used as the CreatedAtAction target for a clean 201 Location.
        [HttpGet("{expenseId:int}")]
        public async Task<IActionResult> GetOne(int groupId, int expenseId)
        {
            if (!await IsMemberAsync(groupId)) return Forbid();
            var exists = await _db.Expenses.AnyAsync(e => e.Id == expenseId && e.GroupId == groupId);
            return exists ? Ok(await BuildDtoAsync(expenseId)) : NotFound();
        }

        private async Task<ExpenseDto> BuildDtoAsync(int expenseId)
        {
            var e = await _db.Expenses
                .Include(x => x.PaidByUser)
                .Include(x => x.Shares).ThenInclude(s => s.User)
                .FirstAsync(x => x.Id == expenseId);

            return new ExpenseDto(
                e.Id, e.GroupId, e.PaidByUserId, e.PaidByUser.Name,
                e.Description, e.Amount, e.Currency, e.AmountInUsdAtEntry, e.CreatedAt,
                e.Shares
                    .OrderBy(s => s.UserId)
                    .Select(s => new ExpenseShareDto(
                        s.Id, s.UserId, s.User.Name, s.ShareAmount, s.IsSettled, s.SettledAt))
                    .ToList());
        }
    }
}
