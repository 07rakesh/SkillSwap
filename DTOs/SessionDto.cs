namespace SkillSwap.API.DTOs;
using System.ComponentModel.DataAnnotations;

public class SessionDto
{
    [Required]
    public int SkillRequestId { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }
}