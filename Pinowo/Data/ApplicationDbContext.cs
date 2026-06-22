using Pinowo.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Pinowo.Data
{
    /// <summary>
    /// EF Core context. Inherits IdentityDbContext so ASP.NET Core Identity's
    /// tables (AspNetUsers/Roles/etc.) live alongside the domain tables, all
    /// keyed by <c>int</c> to match the Section 5 data model. The Identity
    /// user table IS our <see cref="User"/> table - there's only one user table.
    /// </summary>
    public class ApplicationDbContext
        : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Note: Users is already exposed by IdentityDbContext<User, ...>.
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<ExpenseShare> ExpenseShares => Set<ExpenseShare>();
        public DbSet<ExchangeRateSnapshot> ExchangeRateSnapshots => Set<ExchangeRateSnapshot>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Must run first: configures the Identity tables.
            base.OnModelCreating(modelBuilder);

            // Prevent a user from being added to the same group twice.
            modelBuilder.Entity<GroupMember>()
                .HasIndex(gm => new { gm.GroupId, gm.UserId })
                .IsUnique();

            // Email uniqueness is enforced by Identity (RequireUniqueEmail,
            // configured in Program.cs) via the NormalizedEmail index, so we
            // don't add a second manual index here.

            // Avoid cascade-delete cycles (e.g. deleting a User shouldn't
            // cascade-delete Groups they created and orphan other members).
            modelBuilder.Entity<Group>()
                .HasOne(g => g.CreatedByUser)
                .WithMany()
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Expense>()
                .HasOne(e => e.PaidByUser)
                .WithMany(u => u.ExpensesPaid)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
