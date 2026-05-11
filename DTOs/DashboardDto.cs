public class DashboardDto
{
    public int SkillsOffered { get; set; }
    public int SkillsLearning { get; set; }
    public int PendingRequests { get; set; }
    public int ScheduledSessions { get; set; }
    public int Credits { get; set; }

    public List<DashboardRequestDto> RecentRequests { get; set; } = new();
    public List<DashboardSessionDto> UpcomingSessions { get; set; } = new();
}

public class DashboardRequestDto
{
    public int RequestId { get; set; }
    public string SkillTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DashboardSessionDto
{
    public int SessionId { get; set; }
    public string SkillTitle { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public string Status { get; set; } = string.Empty;
}