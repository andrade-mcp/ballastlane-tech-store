using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Services;
using BallastlaneTechStore.Application.Tests.TestSupport;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;
using FluentAssertions;
using NSubstitute;

namespace BallastlaneTechStore.Application.Tests;

public class AuthServiceTests
{
    private readonly InMemoryUserRepo _users = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenIssuer _jwt = Substitute.For<IJwtTokenIssuer>();
    private readonly FixedClock _clock = new();

    private AuthService Sut() => new(_users, _hasher, _jwt, _clock);

    [Fact]
    public async Task Register_persists_user_with_hashed_password()
    {
        _hasher.Hash("Pa55word!").Returns("HASHED");
        await Sut().RegisterAsync(new RegisterRequest("a@x.com", "Pa55word!", "Alice"), default);
        var stored = await _users.GetByEmailAsync("a@x.com", default);
        stored!.PasswordHash.Should().Be("HASHED");
    }

    [Fact]
    public async Task Register_rejects_short_passwords()
    {
        await FluentActions.Invoking(() => Sut().RegisterAsync(new RegisterRequest("a@x.com", "short", "A"), default))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Register_blocks_duplicate_email_case_insensitive()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("HASHED");
        await Sut().RegisterAsync(new RegisterRequest("a@x.com", "Pa55word!", "A"), default);
        await FluentActions.Invoking(() => Sut().RegisterAsync(new RegisterRequest("A@X.com", "Pa55word!", "A2"), default))
            .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Login_returns_token_when_password_matches()
    {
        var u = User.Create("a@x.com", "HASH", "A", UserRole.SalesRep, _clock.UtcNow);
        await _users.AddAsync(u, default);
        _hasher.Verify("Pa55word!", "HASH").Returns(true);
        _jwt.Issue(Arg.Any<User>()).Returns(("tok", _clock.UtcNow.AddHours(1)));

        var resp = await Sut().LoginAsync(new LoginRequest("a@x.com", "Pa55word!"), default);
        resp.Token.Should().Be("tok");
        resp.User.Email.Should().Be("a@x.com");
    }

    [Fact]
    public async Task Login_unknown_user_throws_401()
    {
        await FluentActions.Invoking(() => Sut().LoginAsync(new LoginRequest("nope@x.com", "Pa55word!"), default))
            .Should().ThrowAsync<AuthenticationException>();
    }
}
