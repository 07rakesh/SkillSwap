using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SkillSwap.API.DTOs;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SkillsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SkillsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetMySkills()
    {
        var userId = GetUserId();

        var skills = _context.Skills
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SkillId)
            .ToList();

        return Ok(skills);
    }

    [HttpGet("public")]
    public async Task<IActionResult> GetPublicSkills(string search = "", string category = "")
    {
        var currentUserId = GetUserId();

        var query = _context.Skills
            .Include(s => s.User)
            .Where(s => s.Type == "Offered")
            .Where(s => s.UserId != currentUserId)
            .Where(s => !_context.SkillRequests.Any(r =>
                r.SkillId == s.SkillId &&
                r.RequesterUserId == currentUserId &&
                r.Status == "Accepted"));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s =>
                s.SkillName.Contains(search) ||
                s.Description.Contains(search) ||
                s.User.FullName.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var skills = await query
            .Select(s => new
            {
                s.SkillId,
                s.SkillName,
                s.Description,
                s.Category,
                s.Type,
                s.UserId,
                MentorName = s.User.FullName,
                MentorEmail = s.User.Email
            })
            .OrderByDescending(s => s.SkillId)
            .ToListAsync();

        return Ok(skills);
    }
    [HttpPost]
    public IActionResult CreateSkill([FromBody] SkillDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        var skill = new Skill
        {
            SkillName = model.SkillName,
            Description = model.Description,
            Category = model.Category,
            Type = model.Type,
            UserId = userId
        };

        _context.Skills.Add(skill);
        _context.SaveChanges();

        return Ok(skill);
    }

    [HttpPut("{id}")]
    public IActionResult UpdateSkill(int id, [FromBody] SkillDto model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = GetUserId();

        var skill = _context.Skills.FirstOrDefault(s => s.SkillId == id && s.UserId == userId);

        if (skill == null)
            return NotFound(new { message = "Skill not found" });

        skill.SkillName = model.SkillName;
        skill.Description = model.Description;
        skill.Category = model.Category;
        skill.Type = model.Type;

        _context.SaveChanges();

        return Ok(skill);
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteSkill(int id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized();

        var skill = await _context.Skills.FirstOrDefaultAsync(s => s.SkillId == id);
        if (skill == null)
            return NotFound(new { message = "Skill not found" });

        if (skill.UserId != currentUserId)
            return Forbid();

        var hasRequests = await _context.SkillRequests.AnyAsync(r => r.SkillId == id);
        var hasSessions = await _context.Sessions.AnyAsync(s => s.SkillRequest.SkillId == id);

        if (hasRequests || hasSessions)
        {
            return BadRequest(new
            {
                message = "This skill cannot be deleted because it is linked to requests or sessions."
            });
        }

        _context.Skills.Remove(skill);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Skill deleted successfully" });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(claim))
            throw new UnauthorizedAccessException("Invalid token");

        return int.Parse(claim);
    }
}