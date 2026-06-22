using Pinowo.Data;
using Pinowo.Models.Enums;
using Pinowo.Services.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Pinowo.Services
{
    /// <summary>
    /// THE core business logic of the whole app. Keep this independent of
    /// MVC/SignalR - it should be callable from an API controller, a SignalR
    /// hub, or a unit test with zero changes.
    ///
    /// ALGORITHM (do not deviate without updating the PRD):
    /// 1. Load every ExpenseShare for the group, joined to its parent Expense.
    /// 2. For each ExpenseShare where ShareUserId != Expense.PaidByUserId and
    ///    IsSettled == false:
    ///       - That ShareUserId owes Expense.PaidByUserId an amount equal to
    ///         the share's USD-equivalent value (ShareAmount converted using
    ///         the rate for Expense.Currency, OR - simpler and recommended -
    ///         derive each share's USD value proportionally from
    ///         Expense.AmountInUsdAtEntry, so you don't need historical rates
    ///         per share, only the one snapshot taken at expense-entry time).
    ///       - Accumulate this into a running dictionary keyed by
    ///         (debtorUserId, creditorUserId) -> decimal usdAmount.
    /// 3. NET each pair: if A owes B 30 USD and B owes A 10 USD (from different
    ///    expenses), collapse that into a single edge: A owes B 20 USD.
    ///    Only keep edges where the net amount is non-zero.
    /// 4. Build OverallPositions: for each user, sum (amounts owed TO them)
    ///    minus (amounts they owe), across all their netted edges.
    /// 5. Build SuggestedSettlements directly from the netted pairwise edges
    ///    in step 3 (already minimal for a single group - no further graph
    ///    reduction needed unless you add multi-group netting as a stretch goal).
    /// 6. Convert back to original-currency suggestions only if your UI needs
    ///    "pay X in BTC" - otherwise USD-equivalent display is acceptable for MVP.
    ///
    /// EDGE CASES TO HANDLE:
    /// - Group with one member or zero expenses -> return empty lists, not an error.
    /// - A user who paid for themselves only (no other shares) -> excluded from edges.
    /// - Rounding: do all math in decimal, round only at the final display step
    ///   (2 decimal places for USD) to avoid compounding rounding errors.
    /// </summary>
    public class BalanceCalculatorService : IBalanceCalculatorService
    {
        private readonly ApplicationDbContext _db;
        private readonly IExchangeRateService _rateService;

        public BalanceCalculatorService(ApplicationDbContext db, IExchangeRateService rateService)
        {
            _db = db;
            _rateService = rateService;
        }

        public async Task<GroupBalanceResult> CalculateGroupBalancesAsync(int groupId)
        {
            var result = new GroupBalanceResult { GroupId = groupId };

            // Fetch current rates up front so the whole calculation is consistent
            // and the result can report exactly which rates were used.
            result.RatesUsedUsd[CurrencyType.BTC] = await _rateService.GetUsdRateAsync(CurrencyType.BTC);
            result.RatesUsedUsd[CurrencyType.STABLECOIN] = await _rateService.GetUsdRateAsync(CurrencyType.STABLECOIN);

            // Step 1: load every UNSETTLED share for the group with its parent expense.
            var shares = await _db.ExpenseShares
                .Include(s => s.Expense)
                .Where(s => s.Expense.GroupId == groupId && !s.IsSettled)
                .ToListAsync();

            // Step 2: accumulate directed debt (debtor -> creditor) in USD.
            // Each share's USD value is derived PROPORTIONALLY from the expense's
            // entry-time snapshot, not a fresh rate lookup - so the math is tied
            // to the rate that was in effect when the expense was entered.
            var directed = new Dictionary<(int Debtor, int Creditor), decimal>();
            foreach (var s in shares)
            {
                var e = s.Expense;
                if (s.UserId == e.PaidByUserId) continue; // payer's own share nets out
                if (e.Amount == 0m) continue;             // guard against divide-by-zero

                var shareUsd = e.AmountInUsdAtEntry * (s.ShareAmount / e.Amount);
                var key = (s.UserId, e.PaidByUserId);
                directed[key] = directed.GetValueOrDefault(key) + shareUsd;
            }

            // Step 3: net opposing edges into one signed value per unordered pair.
            // Convention for netPair[(a,b)] with a < b: positive => a owes b.
            var netPair = new Dictionary<(int A, int B), decimal>();
            foreach (var ((debtor, creditor), amount) in directed)
            {
                var a = Math.Min(debtor, creditor);
                var b = Math.Max(debtor, creditor);
                var signed = debtor == a ? amount : -amount;
                netPair[(a, b)] = netPair.GetValueOrDefault((a, b)) + signed;
            }

            // Resolve display names for everyone involved.
            var userIds = netPair.Keys.SelectMany(k => new[] { k.A, k.B }).ToHashSet();
            var names = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name);
            string NameOf(int id) => names.GetValueOrDefault(id, $"#{id}");

            // Steps 4 & 5: build settlement edges + overall positions.
            // Round each pair to 2dp ONCE here (final display step), then derive
            // overall positions from those rounded edges so everything reconciles.
            var overall = new Dictionary<int, decimal>();
            foreach (var ((a, b), raw) in netPair)
            {
                var net = Math.Round(raw, 2, MidpointRounding.AwayFromZero);
                if (net == 0m) continue; // drop settled-out pairs

                var (debtor, creditor, amount) = net > 0m ? (a, b, net) : (b, a, -net);

                result.SuggestedSettlements.Add(new PairwiseBalance
                {
                    OwedByUserId = debtor,
                    OwedByUserName = NameOf(debtor),
                    OwedToUserId = creditor,
                    OwedToUserName = NameOf(creditor),
                    NetAmountUsd = amount
                });

                overall[creditor] = overall.GetValueOrDefault(creditor) + amount;
                overall[debtor] = overall.GetValueOrDefault(debtor) - amount;
            }

            result.SuggestedSettlements = result.SuggestedSettlements
                .OrderByDescending(p => p.NetAmountUsd)
                .ToList();

            result.OverallPositions = overall
                .Select(kv => new UserGroupBalance
                {
                    UserId = kv.Key,
                    UserName = NameOf(kv.Key),
                    NetPositionUsd = kv.Value
                })
                .OrderByDescending(u => u.NetPositionUsd)
                .ToList();

            return result;
        }
    }
}
