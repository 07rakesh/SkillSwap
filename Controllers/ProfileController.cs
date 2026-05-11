using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkillSwapAI.DTOs;
using System.Security.Claims;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(new { message = "User claim not found." });

        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Invalid user id in token." });

        var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(new
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
            sessionsCompleted = user.SessionsCompleted
        });
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(new { message = "User claim not found." });

        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Invalid user id in token." });

        if (dto == null)
            return BadRequest(new { message = "Invalid profile data." });

        var user = await _context.Users.FirstOrDefaultAsync(x => x.UserId == userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (string.IsNullOrWhiteSpace(dto.FullName))
            return BadRequest(new { message = "Full name is required." });

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Email is required." });

        user.FullName = dto.FullName.Trim();
        user.Email = dto.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();
        user.Location = string.IsNullOrWhiteSpace(dto.Location) ? null : dto.Location.Trim();
        user.Bio = string.IsNullOrWhiteSpace(dto.Bio) ? null : dto.Bio.Trim();

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Profile updated successfully",
            user = new
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
                sessionsCompleted = user.SessionsCompleted
            }
        });
    }

    [HttpPost("upload-image")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage([FromForm] UploadProfileImageDto dto)
    {
        var file = dto.File;

        if (file == null || file.Length == 0)
            return BadRequest(new { message = "No file uploaded" });

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            return Unauthorized(new { message = "User claim not found." });

        if (!int.TryParse(userIdClaim, out int userId))
            return Unauthorized(new { message = "Invalid user id in token." });

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return NotFound(new { message = "User not found" });

        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var imageUrl = $"{Request.Scheme}://{Request.Host}/uploads/{fileName}";

        user.ProfileImageUrl = imageUrl;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Image uploaded successfully",
            imageUrl = imageUrl
        });
    }
}