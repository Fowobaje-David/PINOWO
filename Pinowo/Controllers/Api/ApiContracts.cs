using System.ComponentModel.DataAnnotations;
using Pinowo.Models.Enums;

namespace Pinowo.Controllers.Api
{
    // ---- Request bodies ----
    // Plain classes (not records): MVC's validator requires validation
    // metadata on the property, which record primary-ctor parameters don't
    // expose cleanly - using classes keeps [Required] etc. effective.

    public class RegisterRequest
    {
        [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required, MinLength(6)] public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required, EmailAddress] public string Email { get; set; } = string.Empty;
        [Required] public string Password { get; set; } = string.Empty;
    }

    public class CreateGroupRequest
    {
        [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    }

    public class AddMemberRequest
    {
        [Required] public int UserId { get; set; }
    }

    public class CreateExpenseRequest
    {
        [Required, MaxLength(255)] public string Description { get; set; } = string.Empty;
        [Required, Range(0.00000001, 1_000_000_000)] public decimal Amount { get; set; }
        [Required] public CurrencyType Currency { get; set; }
        [Required] public int PaidByUserId { get; set; }
    }

    // ---- Response bodies (DTOs keep EF navigation cycles out of the JSON) ----

    public record UserDto(int Id, string Name, string Email);

    public record GroupMemberDto(int UserId, string Name, string Email, DateTime JoinedAt);

    public record GroupDto(
        int Id,
        string Name,
        int CreatedByUserId,
        DateTime CreatedAt,
        IReadOnlyList<GroupMemberDto> Members);

    public record ExpenseShareDto(
        int Id,
        int UserId,
        string UserName,
        decimal ShareAmount,
        bool IsSettled,
        DateTime? SettledAt);

    public record ExpenseDto(
        int Id,
        int GroupId,
        int PaidByUserId,
        string PaidByName,
        string Description,
        decimal Amount,
        CurrencyType Currency,
        decimal AmountInUsdAtEntry,
        DateTime CreatedAt,
        IReadOnlyList<ExpenseShareDto> Shares);

    public record RateDto(CurrencyType Currency, decimal UsdRate, DateTime FetchedAt);
}
