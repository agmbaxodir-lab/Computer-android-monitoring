using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace FileMonitoring.Api.Controllers;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);
public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

[ApiController, Route("api/v1/auth"), EnableRateLimiting("login")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration cfg) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest r)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.Username == r.Username && x.IsActive);
        if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, r.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid credentials" });
        return Ok(await Issue(u));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest r)
    {
        var h = Hash(r.RefreshToken);
        var t = await db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == h);
        if (t is null || t.RevokedAt != null || t.ExpiresAt < DateTimeOffset.UtcNow) return Unauthorized();
        t.RevokedAt = DateTimeOffset.UtcNow; // rotation: eski token bekor qilinadi
        var u = await db.Users.FindAsync(t.UserId);
        if (u is null || !u.IsActive) return Unauthorized();
        return Ok(await Issue(u));
    }

    private async Task<TokenResponse> Issue(User u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Secret"]!));
        var jwt = new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Name, u.Username), new Claim(ClaimTypes.Role, u.Role)],
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.RefreshTokens.Add(new RefreshToken { Id = Guid.NewGuid(), UserId = u.Id, TokenHash = Hash(refresh), ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
        await db.SaveChangesAsync();
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), refresh, 900);
    }
    private static string Hash(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
}
