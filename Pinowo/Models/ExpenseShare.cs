using System.ComponentModel.DataAnnotations.Schema;

namespace Pinowo.Models
{
    /// <summary>
    /// Represents one user's portion of a single Expense.
    /// This is the row-level unit the BalanceCalculatorService sums over.
    /// </summary>
    public class ExpenseShare
    {
        public int Id { get; set; }

        public int ExpenseId { get; set; }
        public Expense Expense { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        // Same currency as the parent Expense - kept denormalized here so
        // settlement logic never has to join back to Expense just to know units.
        [Column(TypeName = "decimal(18,8)")]
        public decimal ShareAmount { get; set; }

        public bool IsSettled { get; set; } = false;

        // SettledAt doubles as the settlement timestamp. The two fields below are
        // populated only when the settlement also moved funds on-chain.
        public DateTime? SettledAt { get; set; }

        public string? SettlementTxHash { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SettlementTokenAmount { get; set; }
    }
}
