using System.ComponentModel.DataAnnotations;

namespace SkillSwap.API.DTOs;

public class ReviewDto
{
    [Required]
    public int SessionId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Required]
    public string Comment { get; set; } = string.Empty;
}