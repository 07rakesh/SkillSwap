public class Session
{
    public int SessionId { get; set; }

    public int SkillRequestId { get; set; }
    public SkillRequest SkillRequest { get; set; } = null!;

    public DateTime ScheduledAt { get; set; }

    public DateTime SessionDate { get; set; } 
    public DateTime SessionTime { get; set; }

    public string? MeetingLink { get; set; }

    public bool IsStarted { get; set; } = false;

    public string Status { get; set; } = "Scheduled";

    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}