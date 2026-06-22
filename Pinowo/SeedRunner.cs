using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.Models.Enums;
using Pinowo.Services;

namespace Pinowo
{
    /// <summary>
    /// Dev-only demo data seeder. Run with: <c>dotnet run -- seed</c>.
    /// Wipes all domain data and recreates two realistic demo groups. Uses
    /// UserManager (correct password hashing) and the real ExpenseService /
    /// SettlementService so the seeded balances exercise the same code paths.
    /// </summary>
    public static class SeedRunner
    {
        public static async Task RunAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            var userManager = sp.GetRequiredService<UserManager<User>>();
            var expenses = sp.GetRequiredService<IExpenseService>();
            var settlement = sp.GetRequiredService<ISettlementService>();
            var calculator = sp.GetRequiredService<IBalanceCalculatorService>();

            // The demo password is read from the PINOWO_SEED_PASSWORD environment
            // variable so no credential is committed to source control. If it's not
            // set, a random one is generated and printed for this run.
            var demoPassword = Environment.GetEnvironmentVariable("PINOWO_SEED_PASSWORD");
            if (string.IsNullOrWhiteSpace(demoPassword))
            {
                demoPassword = "Demo-" + Guid.NewGuid().ToString("N")[..10];
                Console.WriteLine($"PINOWO_SEED_PASSWORD not set - generated demo password for this run: {demoPassword}");
            }
            else
            {
                Console.WriteLine("Using demo password from PINOWO_SEED_PASSWORD.");
            }

            Console.WriteLine("Wiping existing data (FK-safe order)...");
            await db.ExpenseShares.ExecuteDeleteAsync();
            await db.Expenses.ExecuteDeleteAsync();
            await db.GroupMembers.ExecuteDeleteAsync();
            await db.Groups.ExecuteDeleteAsync();
            await db.Users.ExecuteDeleteAsync();

            async Task<User> NewUser(string name, string email)
            {
                var u = new User { Name = name, UserName = email, Email = email, CreatedAt = DateTime.UtcNow };
                var r = await userManager.CreateAsync(u, demoPassword);
                if (!r.Succeeded)
                    throw new Exception($"create {email}: {string.Join("; ", r.Errors.Select(e => e.Description))}");
                return u;
            }

            async Task AddMembers(Group g, params User[] members)
            {
                foreach (var m in members)
                    db.GroupMembers.Add(new GroupMember { GroupId = g.Id, UserId = m.Id, JoinedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            Console.WriteLine("Creating users...");
            var amara = await NewUser("Amara Okafor", "amara@pinowo.demo");
            var chidi = await NewUser("Chidi Eze", "chidi@pinowo.demo");
            var bisi = await NewUser("Bisi Adeyemi", "bisi@pinowo.demo");
            var tunde = await NewUser("Tunde Bello", "tunde@pinowo.demo");

            // ---- Group A: 3 members, mixed BTC/USDT, one settled + rest outstanding ----
            var groupA = new Group { Name = "Lagos Apartment", CreatedByUserId = amara.Id, CreatedAt = DateTime.UtcNow };
            db.Groups.Add(groupA);
            await db.SaveChangesAsync();
            await AddMembers(groupA, amara, chidi, bisi);

            var deposit = await expenses.AddExpenseAsync(groupA.Id, amara.Id, "Apartment deposit", 0.01m, CurrencyType.BTC);
            await expenses.AddExpenseAsync(groupA.Id, chidi.Id, "Welcome dinner", 150m, CurrencyType.STABLECOIN);
            await expenses.AddExpenseAsync(groupA.Id, bisi.Id, "Airport taxis", 0.0015m, CurrencyType.BTC);
            await expenses.AddExpenseAsync(groupA.Id, amara.Id, "Groceries & supplies", 75m, CurrencyType.STABLECOIN);

            // Chidi pays back his share of the deposit -> one settled share, rest outstanding.
            var chidiDepositShare = await db.ExpenseShares
                .FirstAsync(s => s.ExpenseId == deposit.Id && s.UserId == chidi.Id);
            await settlement.SettleShareAsync(deposit.Id, chidiDepositShare.Id, chidi.Id);

            // ---- Group B: 2 members, fully outstanding ----
            var groupB = new Group { Name = "Concert Weekend", CreatedByUserId = amara.Id, CreatedAt = DateTime.UtcNow };
            db.Groups.Add(groupB);
            await db.SaveChangesAsync();
            await AddMembers(groupB, amara, tunde);

            await expenses.AddExpenseAsync(groupB.Id, amara.Id, "Concert tickets", 200m, CurrencyType.STABLECOIN);
            await expenses.AddExpenseAsync(groupB.Id, tunde.Id, "Drinks & snacks", 0.0008m, CurrencyType.BTC);

            // ---- Verification ----
            Console.WriteLine("\n=== Verification ===");
            Console.WriteLine(
                $"Users={await db.Users.CountAsync()}  Groups={await db.Groups.CountAsync()}  " +
                $"Members={await db.GroupMembers.CountAsync()}  Expenses={await db.Expenses.CountAsync()}  " +
                $"Shares={await db.ExpenseShares.CountAsync()}  Settled={await db.ExpenseShares.CountAsync(s => s.IsSettled)}");

            foreach (var g in await db.Groups.OrderBy(x => x.Id).ToListAsync())
            {
                var b = await calculator.CalculateGroupBalancesAsync(g.Id);
                var sum = b.OverallPositions.Sum(p => p.NetPositionUsd);
                Console.WriteLine($"\nGroup '{g.Name}' (id {g.Id}): positions sum = {sum:0.00} (must be 0.00)");
                foreach (var s in b.SuggestedSettlements)
                    Console.WriteLine($"   {s.OwedByUserName} owes {s.OwedToUserName}: ${s.NetAmountUsd:0.00}");
                if (b.SuggestedSettlements.Count == 0) Console.WriteLine("   (all settled)");
            }

            var orphanShares = await db.ExpenseShares.CountAsync(s => !db.Expenses.Any(e => e.Id == s.ExpenseId));
            var orphanExpenses = await db.Expenses.CountAsync(e => !db.Groups.Any(g => g.Id == e.GroupId));
            var orphanMembers = await db.GroupMembers.CountAsync(m => !db.Users.Any(u => u.Id == m.UserId));
            Console.WriteLine($"\nOrphans -> shares:{orphanShares} expenses:{orphanExpenses} members:{orphanMembers} (all must be 0)");
            Console.WriteLine("\nSeed complete.");
        }
    }
}
