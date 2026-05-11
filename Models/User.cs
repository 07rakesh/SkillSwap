public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public int Credits { get; set; } = 10;
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }

    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public int SessionsCompleted { get; set; } = 0;


    public ICollection<Skill> Skills { get; set; } = new List<Skill>();
    public ICollection<SkillRequest> SentSkillRequests { get; set; } = new List<SkillRequest>();

}