using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pinowo.Models.Enums;

namespace Pinowo.Models
{
    public class Expense
    {
        public int Id { get; set; }

        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        public int PaidByUserId { get; set; }
        public User PaidByUser { get; set; } = null!;

        [Required, MaxLength(255)]
        public string Description { get; set; } = string.Empty;

        // IMPORTANT: decimal(18,8) to safely hold BTC precision (8 decimal places).
        // Never use float/double for money or crypto amounts - rounding errors
        // compound across many expenses and will produce wrong balances.
        [Column(TypeName = "decimal(18,8)")]
        public decimal Amount { get; set; }

        public CurrencyType Currency { get; set; }

        // Snapshot of USD-equivalent value AT THE TIME the expense was entered.
        // This is what lets you compare/aggregate BTC and stablecoin expenses
        // on one balances screen without re-fetching historical rates later.
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountInUsdAtEntry { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public ICollection<ExpenseShare> Shares { get; set; } = new List<ExpenseShare>();
    }
}
