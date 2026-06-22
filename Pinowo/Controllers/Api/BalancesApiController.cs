using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.Services;

namespace Pinowo.Controllers.Api
{
    /// <summary>
    /// REST API for balances (PRD Section 6):
    ///   GET /api/groups/{groupId}/balances → who-owes-whom, net, USD reference.
    /// Member-only. Delegates all math to BalanceCalculatorService (the same
    /// service the SignalR push path uses) so there is one source of truth.
    /// </summary>
    [ApiController]
    [Route("api/groups/{groupId:int}/balances")]
    [Authorize]
    [Produces("application/json")]
    public class BalancesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly IBalanceCalculatorService _calculator;

        public BalancesApiController(
            ApplicationDbContext db,
            UserManager<User> userManager,
            IBalanceCalculatorService calculator)
        {
            _db = db;
            _userManager = userManager;
            _calculator = calculator;
        }

        [HttpGet]
        public async Task<IActionResult> Get(int groupId)
        {
            if (!await _db.Groups.AnyAsync(g => g.Id == groupId)) return NotFound();

            var userId = int.Parse(_userManager.GetUserId(User)!);
            if (!await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
                return Forbid();

            return Ok(await _calculator.CalculateGroupBalancesAsync(groupId));
        }
    }
}
