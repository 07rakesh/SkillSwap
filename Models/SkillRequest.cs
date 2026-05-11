public class SkillRequest
{
    public int Id { get; set; }

    public int SkillId { get; set; }
    public Skill Skill { get; set; } = null!;

    public int RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;

    public int OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;

    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public int? AvailabilitySlotId { get; set; }

    public string? MeetingLink { get; set; }
    public string? MeetingPlatform { get; set; }
    public DateTime? MeetingCreatedAt { get; set; }

    public int ProviderUserId { get; set; }

    public bool IsStarted { get; set; } = false;

    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}   