using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;

namespace Pinowo.Hubs
{
    /// <summary>
    /// Real-time balances channel. Clients call <see cref="JoinGroup"/> to
    /// subscribe to a group's updates; the server adds them to a SignalR group
    /// ONLY after verifying membership (PRD Section 9 - tie connections to the
    /// user's groups so people only get updates for groups they belong to).
    /// When an expense is added, controllers push "BalancesChanged" to the group
    /// and connected clients re-fetch GET /api/groups/{id}/balances.
    /// </summary>
    [Authorize]
    public class BalancesHub : Hub
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public BalancesHub(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public static string GroupName(int groupId) => $"group-{groupId}";

        public async Task JoinGroup(int groupId)
        {
            var userId = int.Parse(_userManager.GetUserId(Context.User!)!);
            var isMember = await _db.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

            if (!isMember)
                throw new HubException("You are not a member of this group.");

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId));
        }

        public Task LeaveGroup(int groupId) =>
            Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));
    }
}
