namespace SkillSwapAI.Services;

public interface IMeetingService
{
    string GenerateMeetingLink(int sessionId);
}

public class MeetingService : IMeetingService
{
    private const string JitsiBaseUrl = "https://meet.jit.si";

    public string GenerateMeetingLink(int sessionId)
    {
        var roomName = $"skillswap-session-{sessionId}-{Guid.NewGuid():N}";
        return $"{JitsiBaseUrl}/{roomName}";
    }
}
