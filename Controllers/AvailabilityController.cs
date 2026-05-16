using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwapAI.DTOs;
using SkillSwapAI.Models;
using SkillSwapAI.Services;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IMeetingService _meetingService;

    public AvailabilityController(ApplicationDbContext context, IMeetingService meetingService)
    {
        _context = context;
        _meetingService = meetingService;
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return null;
        return int.Parse(userIdClaim);
    }

    [HttpPost("offer-slots")]
    public async Task<IActionResult> OfferSlots([FromBody] OfferSlotsDto dto)
    {
        var mentorId = GetCurrentUserId();
        if (mentorId == null) return Unauthorized();

        var request = await _context.SkillRequests
            .FirstOrDefaultAsync(x => x.Id == dto.SkillRequestId);

        if (request == null)
            return NotFound("Skill request not found");

        if (request.OwnerUserId != mentorId.Value)
            return Forbid();

        foreach (var slot in dto.Slots)
        {
            var availability = new MentorAvailability
            {
                MentorUserId = mentorId.Value,
                SkillRequestId = dto.SkillRequestId,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                IsBooked = false,
                Status = "Available"
            };

            _context.MentorAvailabilities.Add(availability);
        }

        request.Status = "TimeOffered";

        await _context.SaveChangesAsync();

        return Ok(new { message = "Slots added successfully" });
    }

    [HttpGet("{requestId}/slots")]
    public async Task<IActionResult> GetSlots(int requestId)
    {
        var slots = await _context.MentorAvailabilities
            .Where(x => x.SkillRequestId == requestId && !x.IsBooked)
            .OrderBy(x => x.StartTime)
            .ToListAsync();

        return Ok(slots);
    }

    [HttpPost("choose-slot/{slotId}")]
    public async Task<IActionResult> ChooseSlot(int slotId)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var slot = await _context.MentorAvailabilities
            .FirstOrDefaultAsync(x => x.Id == slotId);

        if (slot == null)
            return NotFound("Slot not found");

        if (slot.IsBooked)
            return BadRequest("Slot already booked");

        var request = await _context.SkillRequests
            .Include(r => r.Sessions)
            .FirstOrDefaultAsync(x => x.Id == slot.SkillRequestId);

        if (request == null)
            return NotFound("Skill request not found");

        if (request.RequesterUserId != userId.Value)
            return Forbid();

        var learner = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);
        if (learner == null)
            return NotFound("Learner not found.");

        if (learner.Credits < 1)
            return BadRequest("You do not have enough credits to book this session.");

        slot.IsBooked = true;
        slot.BookedByUserId = userId.Value;
        slot.Status = "Booked";

        request.AvailabilitySlotId = slot.Id;
        request.ScheduledStartTime = slot.StartTime;
        request.ScheduledEndTime = slot.EndTime;
        request.Status = "Scheduled";

        // 💰 Use centralized meeting service
        var meetingLink = _meetingService.GenerateMeetingLink(request.Id);

        request.MeetingLink = meetingLink;
        request.MeetingPlatform = "Jitsi";
        request.MeetingCreatedAt = DateTime.UtcNow;

        // 💰 Deduct credit
        learner.Credits -= 1;

        Session? session = await _context.Sessions
            .FirstOrDefaultAsync(s => s.SkillRequestId == request.Id);

        if (session == null)
        {
            session = new Session
            {
                SkillRequestId = request.Id,
                ScheduledAt = slot.StartTime, 
                ScheduledEndTime = slot.EndTime,
                SessionDate = slot.StartTime.Date,
                SessionTime = slot.StartTime,
                MeetingLink = meetingLink,
                IsStarted = false,
                Status = "Scheduled"
            };

            _context.Sessions.Add(session);
        }
        else
        {
            session.ScheduledAt = slot.StartTime; 
            session.ScheduledEndTime = slot.EndTime;
            session.SessionDate = slot.StartTime.Date;
            session.SessionTime = slot.StartTime;
            session.MeetingLink = meetingLink;
            session.IsStarted = false;
            session.Status = "Scheduled";
        }

        // 📝 Log Booking Transaction
        _context.Transactions.Add(new Transaction
        {
            UserId = userId.Value,
            Amount = -1,
            Type = "Booking",
            Description = $"Booked slot for request #{request.Id}",
            RelatedSession = session
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Slot booked successfully and session created",
            slotId = slot.Id,
            startTime = slot.StartTime,
            endTime = slot.EndTime,
            meetingLink = meetingLink,
            meetingPlatform = request.MeetingPlatform,
            sessionId = session.SessionId
        });
    }
}