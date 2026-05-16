using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwap.API.DTOs;
using System.Security.Claims;
using SkillSwapAI.Services;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SkillRequestsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMeetingService _meetingService;

    public SkillRequestsController(ApplicationDbContext context, IMeetingService meetingService)
    {
        _context = context;
        _meetingService = meetingService;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return null;

        return int.Parse(userIdClaim);
    }




    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateRequest([FromBody] CreateSkillRequestDto dto)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("User id not found in token");

            int requesterUserId = int.Parse(userIdClaim);

            var skill = await _context.Skills.FindAsync(dto.SkillId);

            if (skill == null)
                return NotFound("Skill not found");

            if (skill.UserId == requesterUserId)
                return BadRequest("You cannot request your own skill");

            var alreadyExists = await _context.SkillRequests.AnyAsync(r =>
                r.SkillId == dto.SkillId &&
                r.RequesterUserId == requesterUserId &&
                r.Status == "Pending");

            if (alreadyExists)
                return BadRequest("Request already sent for this skill");

            var request = new SkillRequest
            {
                SkillId = skill.SkillId,
                RequesterUserId = requesterUserId,
                OwnerUserId = skill.UserId,
                ProviderUserId = skill.UserId, // keep in sync with OwnerUserId
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.SkillRequests.Add(request);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Skill request sent successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Internal server error",
                error = ex.Message,
                inner = ex.InnerException?.Message
            });
        }
    }

    [HttpGet("incoming")]
    public async Task<IActionResult> GetIncomingRequests()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var requests = await _context.SkillRequests
            .Include(r => r.Skill)
            .Include(r => r.RequesterUser)
            .Include(r => r.Sessions)
            .Where(r => r.Skill.UserId == userId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();



        var result = requests.Select(r =>
        {
            var latestSession = r.Sessions
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefault();

            var now = DateTime.UtcNow;
            var sessionStatus = latestSession?.Status;
            if (latestSession != null && (sessionStatus == "Scheduled" || sessionStatus == "Started") && 
                latestSession.ScheduledEndTime.HasValue && now > latestSession.ScheduledEndTime.Value)
            {
                sessionStatus = "Expired";
            }

            return new
            {
                r.Id,
                skillName = r.Skill.SkillName,
                requesterName = r.RequesterUser.FullName,
                r.Status,
                r.CreatedAt,
                r.ScheduledStartTime,
                r.ScheduledEndTime,
                MeetingLink = sessionStatus == "Expired" ? null : r.MeetingLink,
                r.MeetingPlatform,
                sessionId = latestSession?.SessionId ?? 0,
                sessionMeetingLink = sessionStatus == "Expired" ? null : latestSession?.MeetingLink,
                sessionScheduledAt = latestSession?.ScheduledAt,
                sessionStatus = sessionStatus,
                isStarted = latestSession?.IsStarted ?? false
            };
        });

        return Ok(result);
    }

    [HttpGet("outgoing")]
    public async Task<IActionResult> GetOutgoingRequests()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var requests = await _context.SkillRequests
            .Include(r => r.Skill)
                .ThenInclude(s => s.User)
            .Include(r => r.Sessions)
            .Where(r => r.RequesterUserId == userId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();



        var result = requests.Select(r =>
        {
            var latestSession = r.Sessions
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefault();

            var now = DateTime.UtcNow;
            var sessionStatus = latestSession?.Status;
            if (latestSession != null && (sessionStatus == "Scheduled" || sessionStatus == "Started") && 
                latestSession.ScheduledEndTime.HasValue && now > latestSession.ScheduledEndTime.Value)
            {
                sessionStatus = "Expired";
            }

            return new
            {
                r.Id,
                r.SkillId,
                skillName = r.Skill.SkillName,
                ownerName = r.Skill.User.FullName,
                r.Status,
                r.CreatedAt,
                r.ScheduledStartTime,
                r.ScheduledEndTime,
                MeetingLink = sessionStatus == "Expired" ? null : r.MeetingLink,
                r.MeetingPlatform,
                sessionId = latestSession?.SessionId ?? 0,
                sessionMeetingLink = sessionStatus == "Expired" ? null : (latestSession != null && latestSession.Status == "Started" ? latestSession.MeetingLink : null),
                sessionScheduledAt = latestSession?.ScheduledAt,
                sessionStatus = sessionStatus,
                isStarted = latestSession?.IsStarted ?? false
            };
        });

        return Ok(result);
    }

    [HttpPut("{id}/accept")]
    public async Task<IActionResult> AcceptRequest(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var request = await _context.SkillRequests
            .Include(r => r.Skill)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound("Request not found.");

        if (request.Skill.UserId != userId.Value)
            return Forbid();

        if (request.Status != "Pending")
            return BadRequest("Only pending requests can be accepted.");

        request.Status = "Accepted";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Request accepted successfully." });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
            return Unauthorized();

        var request = await _context.SkillRequests
            .Include(r => r.Skill)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (request == null)
            return NotFound("Request not found.");

        if (request.Skill.UserId != userId.Value)
            return Forbid();

        if (request.Status != "Pending")
            return BadRequest("Only pending requests can be rejected.");

        request.Status = "Rejected";
        await _context.SaveChangesAsync();

        return Ok(new { message = "Request rejected successfully." });
    }
}