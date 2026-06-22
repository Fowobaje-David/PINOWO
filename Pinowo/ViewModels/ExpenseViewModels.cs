using System.ComponentModel.DataAnnotations;
using Pinowo.Models.Enums;

namespace Pinowo.ViewModels
{
    public class AddExpenseViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;

        [Required, MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        [Required, Range(0.00000001, 1_000_000_000)]
        public decimal Amount { get; set; }

        [Required]
        public CurrencyType Currency { get; set; } = CurrencyType.BTC;

        [Required, Display(Name = "Paid by")]
        public int PaidByUserId { get; set; }

        // Populated for the payer dropdown: (userId, name).
        public List<(int Id, string Name)> Members { get; set; } = new();
    }

    public class ExpenseListItem
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public CurrencyType Currency { get; set; }
        public decimal AmountInUsdAtEntry { get; set; }
        public string PaidByName { get; set; } = string.Empty;
        public int ShareCount { get; set; }
        public DateTime CreatedAt { get; set; }

        public string CurrencyLabel => Currency == CurrencyType.BTC ? "BTC" : "USDT";
        public string AmountDisplay => Currency == CurrencyType.BTC
            ? Amount.ToString("0.########")
            : Amount.ToString("0.##");
    }
}
