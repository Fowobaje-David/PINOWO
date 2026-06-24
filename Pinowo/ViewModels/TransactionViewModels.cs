using Pinowo.Models.Enums;

namespace Pinowo.ViewModels
{
    public class TransactionHistoryItem
    {
        public DateTime? SettledAt { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public decimal AmountUsd { get; set; }
        public CurrencyType Currency { get; set; }
        public string? TxHash { get; set; }
        public bool IsIncoming { get; set; }
    }
}
