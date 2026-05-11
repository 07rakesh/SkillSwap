public class Skill
{
    public int SkillId { get; set; }

    public string SkillName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Offered / Learning

    public int UserId { get; set; }
    public User User { get; set; } = null!;
}