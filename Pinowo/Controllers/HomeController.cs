using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.ViewModels;

namespace Pinowo.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<User> _userManager;

        public HomeController(ApplicationDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        /// <summary>
        /// Dashboard: the groups the signed-in user belongs to. (Net position
        /// across groups is added in the balances phase.) Anonymous users get
        /// a simple landing call-to-action.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
                return View("Landing");

            var userId = int.Parse(_userManager.GetUserId(User)!);

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

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() => View();
    }
}
