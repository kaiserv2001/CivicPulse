using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CivicPulse.Core.Entities;
using CivicPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>Register a new account. Returns a JWT valid for 24 hours.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] AuthRequest request, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLower(), ct))
            return Conflict(new { error = "Email already registered." });

        var user = new AppUser { Email = request.Email.ToLower() };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, request.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Ok(new TokenResponse(GenerateJwt(user)));
    }

    /// <summary>Authenticate. Returns a JWT on success or 401 on bad credentials.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] AuthRequest request, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), ct);
        if (user is null)
            return Unauthorized(new { error = "Invalid email or password." });

        var result = new PasswordHasher<AppUser>().VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid email or password." });

        return Ok(new TokenResponse(GenerateJwt(user)));
    }

    private string GenerateJwt(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public record AuthRequest(string Email, string Password);
public record TokenResponse(string Token);
