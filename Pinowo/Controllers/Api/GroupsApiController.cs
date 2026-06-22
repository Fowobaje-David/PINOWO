using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;

namespace Pinowo.Controllers.Api
{
    /// <summary>
    /// REST API for groups + membership (PRD Section 6):
    ///   POST /api/groups
    ///   GET  /api/groups/{id}
    ///   POST /api/groups/{id}/members
    /// Authenticated via the Identity cookie (log in through /api/users/login first).
    /// </summary>
    [ApiController]
    [Route("api/groups")]
    [Authorize]
    [Produces("application/json")]
    public class GroupsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public GroupsApiController(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private int CurrentUserId => int.Parse(_userManager.GetUserId(User)!);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateGroupRequest req)
        {
            var userId = CurrentUserId;
            var group = new Group
            {
                Name = req.Name,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                Members = { new GroupMember { UserId = userId, JoinedAt = DateTime.UtcNow } }
            };

            _db.Groups.Add(group);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = group.Id }, await BuildDtoAsync(group.Id));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var isMember = await _db.GroupMembers.AnyAsync(m => m.GroupId == id && m.UserId == CurrentUserId);
            if (!isMember)
            {
                var exists = await _db.Groups.AnyAsync(g => g.Id == id);
                return exists ? Forbid() : NotFound();
            }

            return Ok(await BuildDtoAsync(id));
        }

        [HttpPost("{id:int}/members")]
        public async Task<IActionResult> AddMember(int id, [FromBody] AddMemberRequest req)
        {
            var group = await _db.Groups.Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == id);
            if (group is null) return NotFound();
            if (!group.Members.Any(m => m.UserId == CurrentUserId)) return Forbid();

            if (group.Members.Any(m => m.UserId == req.UserId))
                return Conflict(new { error = "User is already a member of this group." });

            if (!await _db.Users.AnyAsync(u => u.Id == req.UserId))
                return NotFound(new { error = "User not found." });

            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = id,
                UserId = req.UserId,
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            return Ok(await BuildDtoAsync(id));
        }

        private async Task<GroupDto> BuildDtoAsync(int groupId)
        {
            var group = await _db.Groups
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstAsync(g => g.Id == groupId);

            return new GroupDto(
                group.Id,
                group.Name,
                group.CreatedByUserId,
                group.CreatedAt,
                group.Members
                    .OrderBy(m => m.JoinedAt)
                    .Select(m => new GroupMemberDto(m.UserId, m.User.Name, m.User.Email!, m.JoinedAt))
                    .ToList());
        }
    }
}
