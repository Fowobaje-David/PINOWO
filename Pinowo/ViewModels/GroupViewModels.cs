using System.ComponentModel.DataAnnotations;
using Pinowo.Models;

namespace Pinowo.ViewModels
{
    public class CreateGroupViewModel
    {
        [Required, MaxLength(150), Display(Name = "Group name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Row shown in the dashboard / group list.</summary>
    public class GroupListItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public bool IsCreatedByCurrentUser { get; set; }
    }

    public class GroupDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public List<GroupMember> Members { get; set; } = new();

        // For the "add member" form: existing users not already in the group.
        public List<User> AddableUsers { get; set; } = new();

        // Expenses recorded in this group (most recent first).
        public List<ExpenseListItem> Expenses { get; set; } = new();
    }

    public class AddMemberViewModel
    {
        public int GroupId { get; set; }

        [Required, Display(Name = "User to add")]
        public int UserId { get; set; }
    }
}
