using Pinowo.Models.Enums;

namespace Pinowo.Services.Dtos
{
    /// <summary>
    /// One net relationship between two users within a group.
    /// Convention: a positive Amount means OwedByUserId owes OwedToUserId.
    /// </summary>
    public class PairwiseBalance
    {
        public int OwedByUserId { get; set; }
        public string OwedByUserName { get; set; } = string.Empty;

        public int OwedToUserId { get; set; }
        public string OwedToUserName { get; set; } = string.Empty;

        // Net amount expressed in USD-equivalent - used for comparison/sorting
        // and as the basis for the minimal-transfer settlement suggestion.
        public decimal NetAmountUsd { get; set; }
    }

    /// <summary>
    /// A single user's overall position within a group (sum of all pairwise balances).
    /// Positive = this user is owed money overall. Negative = this user owes overall.
    /// </summary>
    public class UserGroupBalance
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal NetPositionUsd { get; set; }
    }

    /// <summary>
    /// Full payload returned by GET /api/groups/{id}/balances and pushed via SignalR.
    /// </summary>
    public class GroupBalanceResult
    {
        public int GroupId { get; set; }
        public List<UserGroupBalance> OverallPositions { get; set; } = new();
        public List<PairwiseBalance> SuggestedSettlements { get; set; } = new();
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

        // Rates used for this calculation, so the UI can show "as of" info
        // and so you can debug a wrong balance by knowing which rate was applied.
        public Dictionary<CurrencyType, decimal> RatesUsedUsd { get; set; } = new();
    }
}
