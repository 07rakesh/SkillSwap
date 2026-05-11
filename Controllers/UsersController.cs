using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public IActionResult GetMyProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdClaim))
            return Unauthorized(new { message = "Invalid token" });

        var userId = int.Parse(userIdClaim);

        var user = _context.Users
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Role,
                u.Credits
            })
            .FirstOrDefault();

        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserProfile(int id)
    {
        var user = await _context.Users
            .Include(u => u.Skills)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null)
            return NotFound("User not found");

        var result = new
        {
            userId = user.UserId,
            fullName = user.FullName,
            email = user.Email,
            role = user.Role,
            credits = user.Credits,

            profileImageUrl = user.ProfileImageUrl,
            phoneNumber = user.PhoneNumber,
            location = user.Location,
            bio = user.Bio,
            sessionsCompleted = user.SessionsCompleted,

            skillsOffered = user.Skills
                .Where(s => s.Type == "Offered")
                .Select(s => s.SkillName)
                .ToList(),
            skillsWanted = user.Skills
                .Where(s => s.Type == "Learning")
                .Select(s => s.SkillName)
                .ToList()
        };

        return Ok(result);
    }
}