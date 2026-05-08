using System.IdentityModel.Tokens.Jwt;
using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using BallastlaneTechStore.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BallastlaneTechStore.Infrastructure.Tests;

public class PasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    [Fact] public void Round_trips() => _sut.Verify("p", _sut.Hash("p")).Should().BeTrue();

    [Fact] public void Wrong_password_returns_false()
        => _sut.Verify("nope", _sut.Hash("p")).Should().BeFalse();

    [Fact] public void Garbage_hash_returns_false()
        => _sut.Verify("p", "not-a-hash").Should().BeFalse();

    [Fact] public void Two_hashes_of_same_input_differ()
        => _sut.Hash("p").Should().NotBe(_sut.Hash("p"));
}

public class JwtTokenIssuerTests
{
    private readonly JwtSettings _settings = new()
    {
        Issuer = "ballastlane-tech-store",
        Audience = "ballastlane-tech-store-clients",
        SigningKey = "test-only-signing-key-32-bytes-long-pad-pad",
        ExpiresMinutes = 60,
    };
    private readonly IClock _clock = Substitute.For<IClock>();

    public JwtTokenIssuerTests() => _clock.UtcNow.Returns(new DateTime(2026, 5, 8, 9, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Issue_builds_token_with_expected_claims()
    {
        var sut = new JwtTokenIssuer(Options.Create(_settings), _clock);
        var user = User.Create("a@x.com", "HASH", "Alice", UserRole.Manager, _clock.UtcNow);
        var (token, exp) = sut.Issue(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Issuer.Should().Be(_settings.Issuer);
        jwt.Audiences.Should().Contain(_settings.Audience);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        exp.Should().Be(_clock.UtcNow.AddMinutes(_settings.ExpiresMinutes));
    }
}
