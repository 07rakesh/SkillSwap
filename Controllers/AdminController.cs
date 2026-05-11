using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("users")]
    public IActionResult GetAllUsers(int page = 1, int pageSize = 5, string search = "")
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                u.FullName.Contains(search) ||
                u.Email.Contains(search));
        }

        var totalRecords = query.Count();

        var users = query
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.Role,
                u.Credits
            })
            .ToList();

        return Ok(new
        {
            totalRecords,
            page,
            pageSize,
            users
        });
    }

    [HttpDelete("user/{id}")]
    public IActionResult DeleteUser(int id)
    {
        var user = _context.Users.Find(id);

        if (user == null)
            return NotFound(new { message = "User not found" });

        _context.Users.Remove(user);
        _context.SaveChanges();

        return Ok(new { message = "User deleted successfully" });
    }

    [HttpGet("sessions")]
    public IActionResult GetAllSessions()
    {
        var sessions = _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(sr => sr.Skill)
            .Include(s => s.SkillRequest)
                .ThenInclude(sr => sr.RequesterUser)
            .Select(s => new
            {
                s.SessionId,
                s.SkillRequestId,
                LearnerId = s.SkillRequest.RequesterUserId,
                LearnerName = s.SkillRequest.RequesterUser.FullName,
                MentorId = s.SkillRequest.Skill.UserId,
                SkillId = s.SkillRequest.Skill.SkillId,
                SkillName = s.SkillRequest.Skill.SkillName,
                s.Status,
                s.ScheduledAt
            })
            .ToList();

        return Ok(sessions);
    }

    [HttpGet("reviews")]
    public IActionResult GetAllReviews()
    {
        var reviews = _context.Reviews
            .Select(r => new
            {
                r.ReviewId,
                r.TeacherId,
                r.Rating,
                r.Comment,
                r.CreatedAt
            })
            .ToList();

        return Ok(reviews);
    }

    [HttpGet("stats")]
    public IActionResult GetStats()
    {
        var totalUsers = _context.Users.Count();
        var totalSkills = _context.Skills.Count();
        var totalSkillRequests = _context.SkillRequests.Count();
        var totalSessions = _context.Sessions.Count();
        var completedSessions = _context.Sessions.Count(s => s.Status == "Completed");
        var totalReviews = _context.Reviews.Count();

        return Ok(new
        {
            totalUsers,
            totalSkills,
            totalSkillRequests,
            totalSessions,
            completedSessions,
            totalReviews
        });
    }
}