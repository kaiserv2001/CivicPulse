using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CivicPulse.Core.Entities;
using CivicPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProfileController(AppDbContext db) => _db = db;

    private int CurrentUserId =>
        int.Parse(
            User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("User ID claim missing."));

    /// <summary>Returns the authenticated user's email and creation date.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([CurrentUserId], ct);
        if (user is null) return NotFound();
        return Ok(new ProfileResponse(user.Email, user.CreatedAt));
    }

    /// <summary>Update the authenticated user's email address.</summary>
    [HttpPut("email")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailRequest request, CancellationToken ct)
    {
        var userId = CurrentUserId;
        var user = await _db.Users.FindAsync([userId], ct);
        if (user is null) return NotFound();

        var newEmail = request.NewEmail.Trim().ToLower();
        if (await _db.Users.AnyAsync(u => u.Email == newEmail && u.Id != userId, ct))
            return Conflict(new { error = "Email already in use." });

        user.Email = newEmail;
        await _db.SaveChangesAsync(ct);
        return Ok(new ProfileResponse(user.Email, user.CreatedAt));
    }

    /// <summary>Change the authenticated user's password.</summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePassword([FromBody] UpdatePasswordRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync([CurrentUserId], ct);
        if (user is null) return NotFound();

        var hasher = new PasswordHasher<AppUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
            return BadRequest(new { error = "Current password is incorrect." });

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

public record ProfileResponse(string Email, DateTime CreatedAt);
public record UpdateEmailRequest(string NewEmail);
public record UpdatePasswordRequest(string CurrentPassword, string NewPassword);
