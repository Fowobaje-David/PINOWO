using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Pinowo.Models
{
    /// <summary>
    /// Application user. Derives from IdentityUser&lt;int&gt; so ASP.NET Core Identity
    /// manages credentials/password hashing (PRD kickoff #2) while the primary key
    /// stays an <c>int</c> - keeping every FK in the Section 5 data model
    /// (PaidByUserId, CreatedByUserId, GroupMember.UserId, ExpenseShare.UserId)
    /// intact. Identity supplies Id, Email, UserName, PasswordHash, etc., so the
    /// hand-rolled PasswordHash/Email/Id from the original scaffold are gone.
    /// (Deviation from Section 5 approved by product owner: User : IdentityUser&lt;int&gt;.)
    /// </summary>
    public class User : IdentityUser<int>
    {
        // Id, Email, UserName, PasswordHash are inherited from IdentityUser<int>.

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
        public ICollection<Expense> ExpensesPaid { get; set; } = new List<Expense>();
        public ICollection<ExpenseShare> ExpenseShares { get; set; } = new List<ExpenseShare>();
    }
}
