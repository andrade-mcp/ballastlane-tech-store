using System.Net;
using System.Net.Http.Json;
using BallastlaneTechStore.Api.Tests.TestSupport;
using BallastlaneTechStore.Application.Dtos;
using FluentAssertions;

namespace BallastlaneTechStore.Api.Tests;

public class AuthApiTests : IClassFixture<TestApiFactory<AuthApiProgram>>
{
    private readonly HttpClient _client;

    public AuthApiTests(TestApiFactory<AuthApiProgram> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Health_returns_200()
        => (await _client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);

    [Fact]
    public async Task Me_without_token_returns_401()
        => (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Register_then_login_then_me_round_trips()
    {
        var email = $"int-{Guid.NewGuid():N}@x.com";
        (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, "Pa55word!", "Test")))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var login = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Pa55word!"));
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();

        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Authorization = new("Bearer", auth!.Token);
        var me = await _client.SendAsync(req);
        var dto = await me.Content.ReadFromJsonAsync<UserDto>();
        dto!.Email.Should().Be(email);
    }

    [Fact]
    public async Task Login_unknown_user_returns_401()
        => (await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nope@x.com", "whatever1")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Register_short_password_returns_400()
        => (await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("s@x.com", "abc", "S")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
}
