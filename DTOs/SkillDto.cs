using System.ComponentModel.DataAnnotations;
namespace SkillSwap.API.DTOs;

public class SkillDto
{
    [Required]
    public string SkillName { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    [RegularExpression("Offered|Learning", ErrorMessage = "Type must be Offered or Learning")]
    public string Type { get; set; } = string.Empty;
}