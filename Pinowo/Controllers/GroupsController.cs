using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.ViewModels;

namespace Pinowo.Controllers
{
    /// <summary>
    /// Group create/list/detail + membership management (PRD Section 2 #2).
    /// Authorization rule for the MVP: you must be a member of a group to view
    /// or modify it; the creator is auto-added as the first member.
    /// </summary>
    [Authorize]
    public class GroupsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public GroupsController(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private int CurrentUserId => int.Parse(_userManager.GetUserId(User)!);

        // GET /Groups
        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;
            var groups = await _db.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Select(gm => new GroupListItem
                {
                    Id = gm.Group.Id,
                    Name = gm.Group.Name,
                    MemberCount = gm.Group.Members.Count,
                    IsCreatedByCurrentUser = gm.Group.CreatedByUserId == userId
                })
                .ToListAsync();

            return View(groups);
        }

        // GET /Groups/Create
        public IActionResult Create() => View(new CreateGroupViewModel());

        // POST /Groups/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateGroupViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var userId = CurrentUserId;
            var group = new Group
            {
                Name = model.Name,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                // Creator is automatically the first member.
                Members = { new GroupMember { UserId = userId, JoinedAt = DateTime.UtcNow } }
            };

            _db.Groups.Add(group);
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"Group \"{group.Name}\" created.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        // GET /Groups/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userId = CurrentUserId;

            var group = await _db.Groups
                .Include(g => g.CreatedByUser)
                .Include(g => g.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group is null) return NotFound();
            if (!group.Members.Any(m => m.UserId == userId)) return Forbid();

            var memberIds = group.Members.Select(m => m.UserId).ToList();
            var addableUsers = await _db.Users
                .Where(u => !memberIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .ToListAsync();

            var expenses = await _db.Expenses
                .Where(e => e.GroupId == id)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new ExpenseListItem
                {
                    Id = e.Id,
                    Description = e.Description,
                    Amount = e.Amount,
                    Currency = e.Currency,
                    AmountInUsdAtEntry = e.AmountInUsdAtEntry,
                    PaidByName = e.PaidByUser.Name,
                    ShareCount = e.Shares.Count,
                    CreatedAt = e.CreatedAt
                })
                .ToListAsync();

            var vm = new GroupDetailsViewModel
            {
                Id = group.Id,
                Name = group.Name,
                CreatedByName = group.CreatedByUser.Name,
                CreatedAt = group.CreatedAt,
                Members = group.Members.OrderBy(m => m.JoinedAt).ToList(),
                AddableUsers = addableUsers,
                Expenses = expenses
            };

            return View(vm);
        }

        // POST /Groups/AddMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(AddMemberViewModel model)
        {
            var userId = CurrentUserId;

            var group = await _db.Groups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == model.GroupId);

            if (group is null) return NotFound();
            if (!group.Members.Any(m => m.UserId == userId)) return Forbid();

            // Ignore duplicates (also guarded by the unique index in the DB).
            if (group.Members.Any(m => m.UserId == model.UserId))
            {
                TempData["StatusMessage"] = "That user is already in the group.";
                return RedirectToAction(nameof(Details), new { id = model.GroupId });
            }

            var userExists = await _db.Users.AnyAsync(u => u.Id == model.UserId);
            if (!userExists) return NotFound();

            _db.GroupMembers.Add(new GroupMember
            {
                GroupId = model.GroupId,
                UserId = model.UserId,
                JoinedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = "Member added.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }
    }
}
