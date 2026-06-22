using System.Text;
using Microsoft.EntityFrameworkCore;
using Pinowo.Data;
using Pinowo.Models;
using Pinowo.Models.Enums;
using Pinowo.Services;
using Xunit;
using Xunit.Abstractions;

namespace Pinowo.Tests
{
    /// <summary>
    /// PRD kickoff #5: a 3-user, 3-expense fixture whose expected output is
    /// hand-computable, so the product owner can verify the math by hand before
    /// the calculator is wired into the API/UI.
    ///
    /// FIXTURE (group of Alice=1, Bob=2, Carol=3; every expense split equally 3 ways):
    ///   E1  Alice pays  0.003 BTC   (snapshot $300)  → each owes Alice $100
    ///   E2  Bob   pays  60 USDT      (snapshot $60)   → each owes Bob   $20
    ///   E3  Carol pays  90 USDT      (snapshot $90)   → each owes Carol $30
    ///
    /// HAND-COMPUTED NET (USD):
    ///   Alice/Bob :  Bob→Alice 100 − Alice→Bob 20  = Bob owes Alice  $80
    ///   Alice/Carol: Carol→Alice 100 − Alice→Carol 30 = Carol owes Alice $70
    ///   Bob/Carol :  Carol→Bob 20 − Bob→Carol 30   = Bob owes Carol   $10
    ///
    /// OVERALL: Alice +150, Bob −90, Carol −60   (sums to 0)
    /// </summary>
    public class BalanceCalculatorServiceTests
    {
        private readonly ITestOutputHelper _output;
        public BalanceCalculatorServiceTests(ITestOutputHelper output) => _output = output;

        private static ApplicationDbContext NewInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"pinowo-test-{Guid.NewGuid()}")
                .Options;
            return new ApplicationDbContext(options);
        }

        private static void SeedFixture(ApplicationDbContext db)
        {
            var alice = new User { Id = 1, Name = "Alice", UserName = "alice@x.com", Email = "alice@x.com" };
            var bob   = new User { Id = 2, Name = "Bob",   UserName = "bob@x.com",   Email = "bob@x.com" };
            var carol = new User { Id = 3, Name = "Carol", UserName = "carol@x.com", Email = "carol@x.com" };
            db.Users.AddRange(alice, bob, carol);

            var group = new Group { Id = 1, Name = "Trip", CreatedByUserId = 1 };
            db.Groups.Add(group);
            db.GroupMembers.AddRange(
                new GroupMember { Id = 1, GroupId = 1, UserId = 1 },
                new GroupMember { Id = 2, GroupId = 1, UserId = 2 },
                new GroupMember { Id = 3, GroupId = 1, UserId = 3 });

            // E1: Alice pays 0.003 BTC, snapshot $300; each share 0.001 BTC.
            db.Expenses.Add(new Expense
            {
                Id = 1, GroupId = 1, PaidByUserId = 1, Description = "Hotel (BTC)",
                Amount = 0.003m, Currency = CurrencyType.BTC, AmountInUsdAtEntry = 300m,
                Shares =
                {
                    new ExpenseShare { Id = 1, UserId = 1, ShareAmount = 0.001m },
                    new ExpenseShare { Id = 2, UserId = 2, ShareAmount = 0.001m },
                    new ExpenseShare { Id = 3, UserId = 3, ShareAmount = 0.001m },
                }
            });

            // E2: Bob pays 60 USDT, snapshot $60; each share 20.
            db.Expenses.Add(new Expense
            {
                Id = 2, GroupId = 1, PaidByUserId = 2, Description = "Dinner (USDT)",
                Amount = 60m, Currency = CurrencyType.STABLECOIN, AmountInUsdAtEntry = 60m,
                Shares =
                {
                    new ExpenseShare { Id = 4, UserId = 1, ShareAmount = 20m },
                    new ExpenseShare { Id = 5, UserId = 2, ShareAmount = 20m },
                    new ExpenseShare { Id = 6, UserId = 3, ShareAmount = 20m },
                }
            });

            // E3: Carol pays 90 USDT, snapshot $90; each share 30.
            db.Expenses.Add(new Expense
            {
                Id = 3, GroupId = 1, PaidByUserId = 3, Description = "Taxi (USDT)",
                Amount = 90m, Currency = CurrencyType.STABLECOIN, AmountInUsdAtEntry = 90m,
                Shares =
                {
                    new ExpenseShare { Id = 7, UserId = 1, ShareAmount = 30m },
                    new ExpenseShare { Id = 8, UserId = 2, ShareAmount = 30m },
                    new ExpenseShare { Id = 9, UserId = 3, ShareAmount = 30m },
                }
            });

            db.SaveChanges();
        }

        [Fact]
        public async Task ThreeUserThreeExpense_NetsCorrectly()
        {
            using var db = NewInMemoryDb();
            SeedFixture(db);

            var calculator = new BalanceCalculatorService(db, new FakeExchangeRateService());
            var result = await calculator.CalculateGroupBalancesAsync(groupId: 1);

            // ---- Build a human-readable report and persist it next to the test
            //      assembly so the product owner can eyeball expected vs actual. ----
            var report = BuildReport(result);
            _output.WriteLine(report);
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "balance_report.txt"),
                report);

            // ---- Assertions on SuggestedSettlements (positive = OwedBy owes OwedTo) ----
            Assert.Equal(3, result.SuggestedSettlements.Count);
            AssertOwes(result, debtor: "Bob",   creditor: "Alice", usd: 80m);
            AssertOwes(result, debtor: "Carol", creditor: "Alice", usd: 70m);
            AssertOwes(result, debtor: "Bob",   creditor: "Carol", usd: 10m);

            // ---- Assertions on OverallPositions ----
            Assert.Equal(150m, PositionOf(result, "Alice"));
            Assert.Equal(-90m, PositionOf(result, "Bob"));
            Assert.Equal(-60m, PositionOf(result, "Carol"));

            // Overall positions must always net to zero.
            Assert.Equal(0m, result.OverallPositions.Sum(p => p.NetPositionUsd));
        }

        [Fact]
        public async Task EmptyGroup_ReturnsEmptyLists_NotError()
        {
            using var db = NewInMemoryDb();
            var group = new Group { Id = 1, Name = "Empty", CreatedByUserId = 1 };
            db.Groups.Add(group);
            db.SaveChanges();

            var calculator = new BalanceCalculatorService(db, new FakeExchangeRateService());
            var result = await calculator.CalculateGroupBalancesAsync(1);

            Assert.Empty(result.SuggestedSettlements);
            Assert.Empty(result.OverallPositions);
        }

        [Theory]
        [InlineData(0.30000001, 2)]  // odd satoshi
        [InlineData(0.10, 3)]        // doesn't divide evenly
        [InlineData(100, 7)]         // larger remainder
        public void EqualSplit_AlwaysSumsToExactlyTheAmount(decimal amount, int members)
        {
            var shares = IExpenseService.EqualSplit(amount, members);
            Assert.Equal(members, shares.Count);
            Assert.Equal(amount, shares.Sum()); // no satoshi created or lost
        }

        private static void AssertOwes(Services.Dtos.GroupBalanceResult r, string debtor, string creditor, decimal usd)
        {
            var edge = r.SuggestedSettlements.SingleOrDefault(
                p => p.OwedByUserName == debtor && p.OwedToUserName == creditor);
            Assert.True(edge is not null, $"Expected edge: {debtor} owes {creditor}");
            Assert.Equal(usd, edge!.NetAmountUsd);
        }

        private static decimal PositionOf(Services.Dtos.GroupBalanceResult r, string name) =>
            r.OverallPositions.Single(p => p.UserName == name).NetPositionUsd;

        private static string BuildReport(Services.Dtos.GroupBalanceResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Pinowo BalanceCalculatorService - actual output ===");
            sb.AppendLine($"Group {r.GroupId}");
            sb.AppendLine();
            sb.AppendLine("Suggested settlements (who pays whom, USD-equivalent):");
            foreach (var p in r.SuggestedSettlements)
                sb.AppendLine($"  {p.OwedByUserName,-6} owes {p.OwedToUserName,-6} ${p.NetAmountUsd,8:0.00}");
            sb.AppendLine();
            sb.AppendLine("Overall positions (+ owed to them / − they owe):");
            foreach (var u in r.OverallPositions)
                sb.AppendLine($"  {u.UserName,-6} {(u.NetPositionUsd >= 0 ? "+" : "−")}${Math.Abs(u.NetPositionUsd),8:0.00}");
            sb.AppendLine($"  (sum = {r.OverallPositions.Sum(p => p.NetPositionUsd):0.00})");
            return sb.ToString();
        }
    }
}
