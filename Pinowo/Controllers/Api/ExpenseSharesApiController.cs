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
    /// REST API for settling a share (PRD Section 6):
    ///   POST /api/expenses/{expenseId}/shares/{shareId}/settle
    /// On success, pushes "BalancesChanged" so live panels drop the settled debt.
    /// </summary>
    [ApiController]
    [Route("api/expenses/{expenseId:int}/shares/{shareId:int}")]
    [Authorize]
    [Produces("application/json")]
    public class ExpenseSharesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly ISettlementService _settlement;
        private readonly IHubContext<BalancesHub> _hub;

        public ExpenseSharesApiController(
            ApplicationDbContext db,
            UserManager<User> userManager,
            ISettlementService settlement,
            IHubContext<BalancesHub> hub)
        {
            _db = db;
            _userManager = userManager;
            _settlement = settlement;
            _hub = hub;
        }

        [HttpPost("settle")]
        public async Task<IActionResult> Settle(int expenseId, int shareId)
        {
            var userId = int.Parse(_userManager.GetUserId(User)!);
            var result = await _settlement.SettleShareAsync(expenseId, shareId, userId);

            switch (result.Outcome)
            {
                case SettleOutcome.NotFound:
                    return NotFound();
                case SettleOutcome.Forbidden:
                    return Forbid();
                case SettleOutcome.AlreadySettled:
                    return Conflict(new { error = "Share is already settled." });
            }

            await _hub.Clients.Group(BalancesHub.GroupName(result.GroupId))
                .SendAsync("BalancesChanged", result.GroupId);

            var share = await _db.ExpenseShares
                .Include(s => s.User)
                .FirstAsync(s => s.Id == shareId);

            return Ok(new ExpenseShareDto(
                share.Id, share.UserId, share.User.Name,
                share.ShareAmount, share.IsSettled, share.SettledAt));
        }
    }
}
