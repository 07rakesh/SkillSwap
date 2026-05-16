using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwapAI.DTOs;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MessagesController(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================
    // SEND MESSAGE
    // =========================
    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        int senderId = int.Parse(userIdClaim);

        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = dto.ReceiverId,
            Content = dto.Content
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return Ok(message);
    }

    // =========================
    // GET CONVERSATION
    // =========================
    [HttpGet("conversation/{userId}")]
    public async Task<IActionResult> GetConversation(int userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        var messages = await _context.Messages
            .Where(m =>
                (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                (m.SenderId == userId && m.ReceiverId == currentUserId))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        // 👁️ Mark as read
        var unread = messages.Where(m => m.ReceiverId == currentUserId && !m.IsRead).ToList();
        if (unread.Any())
        {
            unread.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();
        }

        return Ok(messages);
    }

    // =========================
    // GET UNREAD COUNT
    // =========================
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        var count = await _context.Messages
            .CountAsync(m => m.ReceiverId == currentUserId && !m.IsRead);

        return Ok(new { count });
    }

    // =========================
    // MARK AS READ
    // =========================
    [HttpPost("read/{senderId}")]
    public async Task<IActionResult> MarkAsRead(int senderId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        var unread = await _context.Messages
            .Where(m => m.SenderId == senderId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync();

        if (unread.Any())
        {
            unread.ForEach(m => m.IsRead = true);
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = "Messages marked as read" });
    }

    // =========================
    // GET USERS FOR CHAT
    // =========================
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized();

        int currentUserId = int.Parse(userIdClaim);

        var users = await _context.Users
            .Where(u => u.UserId != currentUserId)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                unreadCount = _context.Messages.Count(m => m.SenderId == u.UserId && m.ReceiverId == currentUserId && !m.IsRead)
            })
            .ToListAsync();

        return Ok(users);
    }
}