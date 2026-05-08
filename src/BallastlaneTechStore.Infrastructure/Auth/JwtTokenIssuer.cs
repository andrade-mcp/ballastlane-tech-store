using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BallastlaneTechStore.Infrastructure.Auth;

public sealed class JwtTokenIssuer : IJwtTokenIssuer
{
    private readonly JwtSettings _settings;
    private readonly IClock _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenIssuer(IOptions<JwtSettings> settings, IClock clock)
    {
        _settings = settings.Value;
        _clock = clock;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        _credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string Token, DateTime ExpiresAt) Issue(User user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_settings.ExpiresMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims, now, expires, _credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
