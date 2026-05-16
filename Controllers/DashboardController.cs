using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetCounts()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Invalid user token." });

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null)
            return NotFound(new { message = "User not found." });

        var skillsOffered = await _context.Skills
    .CountAsync(s => s.UserId == userId && s.Type == "Offered");

        var skillsLearning = await _context.Skills
    .CountAsync(s => s.UserId == userId && s.Type == "Learning");

        var pendingRequests = await _context.SkillRequests
            .CountAsync(r =>
                (r.OwnerUserId == userId || r.RequesterUserId == userId) &&
                r.Status == "Pending");

        var now = DateTime.UtcNow;
        var scheduledSessions = await _context.Sessions
            .Include(s => s.SkillRequest)
            .CountAsync(s =>
                s.SkillRequest != null &&
                (s.SkillRequest.RequesterUserId == userId ||
                 s.SkillRequest.OwnerUserId == userId) &&
                (s.Status == "Scheduled" || s.Status == "Started") &&
                (!s.ScheduledEndTime.HasValue || s.ScheduledEndTime > now));

        var unreadMessagesCount = await _context.Messages
            .CountAsync(m => m.ReceiverId == userId && !m.IsRead);

        var recentRequests = await _context.SkillRequests
            .Include(r => r.Skill)
            .Where(r =>
                r.RequesterUserId == userId ||
                r.OwnerUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .Select(r => new
            {
                r.RequesterUserId,
                SkillTitle = r.Skill != null ? r.Skill.SkillName : "Unknown",
                r.Status,
                r.CreatedAt
            })
            .ToListAsync();

        var upcomingSessions = await _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(sr => sr.Skill)
            .Where(s =>
                s.SkillRequest != null &&
                (s.SkillRequest.RequesterUserId == userId ||
                 s.SkillRequest.OwnerUserId == userId) &&
                (s.Status == "Scheduled" || s.Status == "Started") &&
                s.ScheduledAt >= now.AddMinutes(-30) &&
                (!s.ScheduledEndTime.HasValue || s.ScheduledEndTime > now))
            .OrderBy(s => s.ScheduledAt)
            .Take(5)
            .Select(s => new
            {
                s.SessionId,
                SkillTitle = s.SkillRequest != null && s.SkillRequest.Skill != null
                    ? s.SkillRequest.Skill.SkillName
                    : "Unknown",
                s.SessionDate,
                s.Status
            })
            .ToListAsync();

        return Ok(new
        {
            SkillsOffered = skillsOffered,
            SkillsLearning = skillsLearning,
            PendingRequests = pendingRequests,
            ScheduledSessions = scheduledSessions,
            Credits = user.Credits,
            UnreadMessagesCount = unreadMessagesCount,
            RecentRequests = recentRequests,
            UpcomingSessions = upcomingSessions,
            UserName = user.FullName
        });
    }
}