using System.Security.Claims;
using CivicPulse.Core.Entities;
using CivicPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CivicPulse.API.Controllers;

[ApiController]
[Route("api/push")]
public class PushController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet("vapid-public-key")]
    public IActionResult GetVapidPublicKey() =>
        Ok(new { publicKey = config["Vapid:PublicKey"] });

    [HttpPost("subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var existing = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint, ct);

        if (existing is not null) return NoContent();

        db.PushSubscriptions.Add(new PushSubscription
        {
            UserId = userId,
            Endpoint = req.Endpoint,
            P256dh = req.P256dh,
            Auth = req.Auth
        });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("subscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest req, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var sub = await db.PushSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == req.Endpoint, ct);

        if (sub is null) return NoContent();

        db.PushSubscriptions.Remove(sub);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    public record SubscribeRequest(string Endpoint, string P256dh, string Auth);
    public record UnsubscribeRequest(string Endpoint);
}
