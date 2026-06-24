namespace Pinowo.Services
{
    public enum SettleOutcome
    {
        Settled,
        NotFound,
        Forbidden,
        AlreadySettled
    }

    /// <summary>
    /// Result of attempting to settle a share. GroupId is returned (when known)
    /// so the caller can push a SignalR "BalancesChanged" to the right group.
    /// </summary>
    public record SettleResult(
        SettleOutcome Outcome,
        int GroupId,
        string? TxHash = null,
        string? ExplorerUrl = null,
        decimal? AmountUsd = null);

    public interface ISettlementService
    {
        /// <summary>
        /// Marks a single ExpenseShare as settled. Allowed only for the share's
        /// debtor (the one who owes) or the expense payer (the creditor), and only
        /// if they belong to the group. Idempotency: a second settle returns
        /// <see cref="SettleOutcome.AlreadySettled"/> rather than re-settling.
        /// </summary>
        Task<SettleResult> SettleShareAsync(int expenseId, int shareId, int actingUserId);
    }
}
