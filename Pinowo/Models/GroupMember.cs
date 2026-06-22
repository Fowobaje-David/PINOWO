namespace Pinowo.Models
{
    /// <summary>
    /// Join entity: which users belong to which groups.
    /// </summary>
    public class GroupMember
    {
        public int Id { get; set; }

        public int GroupId { get; set; }
        public Group Group { get; set; } = null!;

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
