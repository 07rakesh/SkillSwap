using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using SkillSwap.API.DTOs;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ReviewsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult GetAllReviews()
    {
        var reviews = _context.Reviews
            .Select(r => new
            {
                r.ReviewId,
                r.SessionId,
                r.ReviewerId,
                r.TeacherId,
                r.Rating,
                r.Comment,
                r.CreatedAt
            })
            .ToList();

        return Ok(reviews);
    }

    [HttpPost]
    public IActionResult CreateReview(ReviewDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var reviewerId = GetUserId();

        var session = _context.Sessions
            .Include(s => s.SkillRequest)
                .ThenInclude(sr => sr.Skill)
            .Include(s => s.SkillRequest)
                .ThenInclude(sr => sr.RequesterUser)
            .FirstOrDefault(s => s.SessionId == dto.SessionId);

        if (session == null)
            return NotFound(new { message = "Session not found" });

        if (session.Status != "Completed")
            return BadRequest(new { message = "Review can only be added after session is completed" });

        if (session.SkillRequest.RequesterUserId != reviewerId)
            return Forbid();

        var existingReview = _context.Reviews
            .FirstOrDefault(r => r.SessionId == dto.SessionId && r.ReviewerId == reviewerId);

        if (existingReview != null)
            return BadRequest(new { message = "You have already reviewed this session" });

        var review = new Review
        {
            SessionId = dto.SessionId,
            ReviewerId = reviewerId,
            TeacherId = session.SkillRequest.Skill.UserId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };

        _context.Reviews.Add(review);
        _context.SaveChanges();

        return Ok(new
        {
            message = "Review added successfully",
            review
        });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(claim))
            throw new UnauthorizedAccessException("Invalid token");

        return int.Parse(claim);
    }
}