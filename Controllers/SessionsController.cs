using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SkillSwap.API.DTOs;
using SkillSwapAI.Services;
using SkillSwapAI.Models;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMeetingService _meetingService;

    public SessionsController(ApplicationDbContext context, IMeetingService meetingService)
    {
        _context = context;
        _meetingService = meetingService;
    }




    [HttpPost]
    public async Task<IActionResult> CreateSession([FromBody] SessionDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User claim not found.");

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized("Invalid user id in token.");

        if (dto == null)
            return BadRequest("Session data is required.");

        if (dto.SkillRequestId <= 0)
            return BadRequest("Valid skill request id is required.");

        if (dto.ScheduledAt == default)
            return BadRequest("Please select date and time");

        if (dto.ScheduledAt <= DateTime.UtcNow)
            return BadRequest("You cannot book a session in the past.");

        var skillRequest = await _context.SkillRequests
            .Include(r => r.Skill)
            .Include(r => r.RequesterUser)
            .Include(r => r.OwnerUser)
            .FirstOrDefaultAsync(r => r.Id == dto.SkillRequestId);

        if (skillRequest == null)
            return NotFound("Skill request not found.");

        if (skillRequest.RequesterUserId != currentUserId)
            return BadRequest("You can only book a session for your own accepted request.");

        if (skillRequest.Status != "Accepted")
            return BadRequest("Session booking is allowed only after request is accepted.");

        if (skillRequest.Skill == null)
            return BadRequest("Skill not found for this request.");

        if (skillRequest.Skill.UserId == currentUserId)
            return BadRequest("You cannot book your own skill");

        var learner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == currentUserId);
        if (learner == null)
            return NotFound("Learner not found.");

        if (learner.Credits < 1)
            return BadRequest("You do not have enough credits to book this session.");

        var existingSession = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SkillRequestId == dto.SkillRequestId);

        if (existingSession != null)
            return BadRequest("Session already booked for this request.");

        var session = new Session
        {
            SkillRequestId = dto.SkillRequestId,
            ScheduledAt = dto.ScheduledAt,
            SessionDate = dto.ScheduledAt.Date,
            MeetingLink = null,
            IsStarted = false,
            Status = "Scheduled"
        };

        // 💰 Deduct credit at booking to prevent over-booking
        learner.Credits -= 1;

        _context.Sessions.Add(session);

        // 📝 Log Booking Transaction
        _context.Transactions.Add(new Transaction
        {
            UserId = currentUserId,
            Amount = -1,
            Type = "Booking",
            Description = $"Booked session for skill: {skillRequest.Skill.SkillName}",
            RelatedSession = session
        });

        skillRequest.Status = "Scheduled";
        skillRequest.IsStarted = false;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Session booked successfully.",
            sessionId = session.SessionId,
            skillRequestId = session.SkillRequestId,
            scheduledAt = session.ScheduledAt,
            sessionDate = session.SessionDate,
            status = session.Status,
            meetingLink = session.MeetingLink,
            meetingPlatform = "Jitsi",
            learnerCredits = learner.Credits
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetSessionHistory()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User claim not found.");

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized("Invalid user id in token.");

        var sessions = await _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.Skill)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.RequesterUser)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.OwnerUser)
            .Where(s =>
                (s.SkillRequest.RequesterUserId == currentUserId ||
                 s.SkillRequest.OwnerUserId == currentUserId) &&
                (s.Status == "Completed" || s.Status == "Cancelled" || s.Status == "Expired" || s.Status == "Started"))
            .OrderByDescending(s => s.ScheduledAt)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var result = sessions.Select(s => {
            var status = s.Status;
            var endTime = s.ScheduledEndTime;
            
            // Check for dynamic expiration if it was still "Scheduled" or "Started" in DB but time has passed
            if ((status == "Scheduled" || status == "Started") && endTime.HasValue && now > endTime.Value)
            {
                status = "Expired";
            }

            return new
            {
                s.SessionId,
                s.SkillRequestId,
                SkillId = s.SkillRequest.Skill.SkillId,
                SkillName = s.SkillRequest.Skill.SkillName,
                LearnerId = s.SkillRequest.RequesterUserId,
                LearnerName = s.SkillRequest.RequesterUser.FullName,
                MentorId = s.SkillRequest.OwnerUserId,
                MentorName = s.SkillRequest.OwnerUser.FullName,
                ScheduledAt = s.ScheduledAt,
                SessionDate = s.SessionDate,
                Status = status,
                MeetingLink = s.MeetingLink,
                MeetingPlatform = "Jitsi",
                IsStarted = s.IsStarted
            };
        });

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetMySessions()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User claim not found.");

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized("Invalid user id in token.");

        var sessions = await _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.Skill)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.RequesterUser)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.OwnerUser)
            .Where(s =>
                (s.SkillRequest.RequesterUserId == currentUserId ||
                 s.SkillRequest.OwnerUserId == currentUserId) &&
                (s.Status == "Scheduled" || s.Status == "Started"))
            .OrderByDescending(s => s.ScheduledAt)
            .ToListAsync();


        var now = DateTime.UtcNow;
        var result = sessions.Select(s => {
            var status = s.Status;
            var endTime = s.ScheduledEndTime;
            
            if ((status == "Scheduled" || status == "Started") && endTime.HasValue && now > endTime.Value)
            {
                status = "Expired";
            }

            return new
            {
                s.SessionId,
                s.SkillRequestId,
                SkillId = s.SkillRequest.Skill.SkillId,
                SkillName = s.SkillRequest.Skill.SkillName,
                LearnerId = s.SkillRequest.RequesterUserId,
                LearnerName = s.SkillRequest.RequesterUser.FullName,
                MentorId = s.SkillRequest.OwnerUserId,
                MentorName = s.SkillRequest.OwnerUser.FullName,
                ScheduledAt = s.ScheduledAt,
                SessionDate = s.SessionDate,
                Status = status,
                MeetingLink = s.MeetingLink,
                MeetingPlatform = "Jitsi",
                IsStarted = s.IsStarted
            };
        });

        return Ok(result);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateSessionStatus(int id, [FromQuery] string status)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized("User claim not found.");

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized("Invalid user id in token.");

        if (id <= 0)
            return BadRequest("Invalid session id.");

        if (string.IsNullOrWhiteSpace(status))
            return BadRequest("Status is required.");

        var allowedStatuses = new[] { "Scheduled", "Cancelled", "Completed" };
        if (!allowedStatuses.Contains(status))
            return BadRequest("Invalid status value. Please use the dedicated 'Start' endpoint to begin a session.");

        var session = await _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.Skill)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.RequesterUser)
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.OwnerUser)
            .FirstOrDefaultAsync(s => s.SessionId == id);

        if (session == null)
            return NotFound("Session not found.");

        var learnerId = session.SkillRequest.RequesterUserId;
        var mentorId = session.SkillRequest.Skill.UserId;

        if (learnerId != currentUserId && mentorId != currentUserId)
            return Forbid();

        if (session.Status == "Cancelled" || session.Status == "Completed")
            return BadRequest("This session can no longer be updated.");

        // ✅ COMPLETE SESSION LOGIC
        if (status == "Completed")
        {
            if (session.Status != "Scheduled" && session.Status != "Started")
                return BadRequest("Only scheduled or started sessions can be marked as completed.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var mentor = await _context.Users.FirstOrDefaultAsync(u => u.UserId == mentorId);

                if (mentor == null)
                    return NotFound("Mentor not found.");

                // 💰 Credit transfer: Learner already paid at booking, now mentor receives it.
                mentor.Credits += 1;
                mentor.SessionsCompleted += 1;

                session.Status = "Completed";
                session.IsStarted = false;
                session.SkillRequest.Status = "Completed";
                session.SkillRequest.IsStarted = false;

                // 📝 Log Earned Transaction
                _context.Transactions.Add(new Transaction
                {
                    UserId = mentorId,
                    Amount = 1,
                    Type = "Earned",
                    Description = $"Earned credit for teaching session: {session.SkillRequest.Skill.SkillName}",
                    RelatedSessionId = session.SessionId
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Session completed successfully. Credits transferred.",
                    sessionId = session.SessionId,
                    status = session.Status,
                    mentorCredits = mentor.Credits,
                    sessionsCompleted = mentor.SessionsCompleted
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred during credit transfer.");
            }
        }

        if (status == "Cancelled")
        {
            if (currentUserId == learnerId)
            {
                var now = DateTime.UtcNow;
                if (session.ScheduledAt < now.AddHours(6))
                {
                    return BadRequest("You can only cancel a session up to 6 hours before its start time.");
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var learner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == learnerId);
                if (learner != null)
                {
                    learner.Credits += 1;
                }

                session.Status = "Cancelled";
                session.IsStarted = false;
                session.SkillRequest.Status = "Accepted";
                session.SkillRequest.IsStarted = false;

                _context.Transactions.Add(new Transaction
                {
                    UserId = learnerId,
                    Amount = 1,
                    Type = "Refund",
                    Description = $"Refunded credit for cancelled session: {session.SkillRequest.Skill.SkillName}",
                    RelatedSessionId = session.SessionId
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "Session cancelled successfully. Credit refunded."
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "An error occurred during cancellation.");
            }
        }


        session.Status = status;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Session status updated successfully.",
            sessionId = session.SessionId,
            status = session.Status
        });
    }

    [HttpPost("start/{id}")]
    public async Task<IActionResult> StartSession(int id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(new { message = "User claim not found." });

        if (!int.TryParse(userIdClaim, out int currentUserId))
            return Unauthorized(new { message = "Invalid user id in token." });

        var session = await _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(r => r.Skill)
            .FirstOrDefaultAsync(s => s.SessionId == id);

        if (session == null)
            return NotFound(new { message = "Session not found" });

        if (session.SkillRequest == null || session.SkillRequest.Skill == null)
            return BadRequest(new { message = "Invalid session data" });

        if (session.SkillRequest.Skill.UserId != currentUserId)
            return Forbid();

        if (session.Status != "Scheduled")
            return BadRequest(new { message = "Only scheduled sessions can be started" });

        var now = DateTime.UtcNow;
        var startTime = session.ScheduledAt;
        var endTime = session.ScheduledEndTime;

        // Allow starting up to 30 minutes before the scheduled time
        if (now < startTime.AddMinutes(-30))
        {
            return BadRequest(new { message = $"You can only start the session 30 minutes before the scheduled time ({startTime:HH:mm})." });
        }

        // Expire if it's past the end time
        if (endTime.HasValue && now > endTime.Value)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                session.Status = "Expired";
                session.SkillRequest.Status = "Expired";

                // 💰 Escrow Refund: Session expired without starting
                var learner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == session.SkillRequest.RequesterUserId);
                if (learner != null)
                {
                    learner.Credits += 1;
                    _context.Transactions.Add(new Transaction
                    {
                        UserId = learner.UserId,
                        Amount = 1,
                        Type = "Refund",
                        Description = $"Refunded credit for expired session: {session.SkillRequest.Skill.SkillName}",
                        RelatedSessionId = session.SessionId
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
            }

            return BadRequest(new { message = "This session has expired and can no longer be started." });
        }

        if (string.IsNullOrWhiteSpace(session.MeetingLink))
        {
            session.MeetingLink = _meetingService.GenerateMeetingLink(session.SessionId);
        }

        session.Status = "Started";
        session.IsStarted = true;
        session.SkillRequest.IsStarted = true;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Session started successfully",
            sessionId = session.SessionId,
            skillRequestId = session.SkillRequestId,
            status = session.Status,
            meetingLink = session.MeetingLink,
            meetingPlatform = "Jitsi"
        });
    }
}