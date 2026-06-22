using Pinowo.Models.Enums;
using Pinowo.Services.Dtos;

namespace Pinowo.ViewModels
{
    /// <summary>One outstanding share the current user owes (a settleable item).</summary>
    public class OutstandingShareItem
    {
        public int ExpenseId { get; set; }
        public int ShareId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string CreditorName { get; set; } = string.Empty;
        public decimal ShareAmount { get; set; }
        public CurrencyType Currency { get; set; }
        public decimal ApproxUsd { get; set; }

        public string CurrencyLabel => Currency == CurrencyType.BTC ? "BTC" : "USDT";
        public string AmountDisplay => Currency == CurrencyType.BTC
            ? ShareAmount.ToString("0.########")
            : ShareAmount.ToString("0.##");
    }

    public class SettleUpViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        /// <summary>Minimal who-pays-whom suggestions (USD-equivalent).</summary>
        public List<PairwiseBalance> SuggestedSettlements { get; set; } = new();

        /// <summary>The current user's own unsettled debts, each markable settled.</summary>
        public List<OutstandingShareItem> MyDebts { get; set; } = new();
    }
}
