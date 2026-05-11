namespace SkillSwap.API.DTOs;

public class PublicUserProfileDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string Bio { get; set; }
    public string ProfileImageUrl { get; set; } // 👈 ADD THIS
    public List<string> SkillsOffered { get; set; } = new();
    public List<string> SkillsWanted { get; set; } = new();
}

