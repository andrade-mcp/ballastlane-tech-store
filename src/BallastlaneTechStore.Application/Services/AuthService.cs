using BallastlaneTechStore.Application.Abstractions;
using BallastlaneTechStore.Application.Common;
using BallastlaneTechStore.Application.Dtos;
using BallastlaneTechStore.Application.Mapping;
using BallastlaneTechStore.Domain.Entities;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Application.Services;

public interface IAuthService
{
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenIssuer _jwt;
    private readonly IClock _clock;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenIssuer jwt, IClock clock)
    {
        _users = users; _hasher = hasher; _jwt = jwt; _clock = clock;
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ValidationException("Password must be at least 8 characters.");

        var email = User.NormaliseEmail(request.Email);
        if (await _users.GetByEmailAsync(email, ct) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = User.Create(email, _hasher.Hash(request.Password), request.DisplayName, UserRole.SalesRep, _clock.UtcNow);
        await _users.AddAsync(user, ct);
        return user.ToDto();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = User.NormaliseEmail(request.Email);
        var user = await _users.GetByEmailAsync(email, ct)
            ?? throw new AuthenticationException("Invalid credentials.");
        if (!_hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthenticationException("Invalid credentials.");

        var (token, expiresAt) = _jwt.Issue(user);
        return new AuthResponse(token, expiresAt, user.ToDto());
    }

    public async Task<UserDto> GetProfileAsync(Guid userId, CancellationToken ct)
        => (await _users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("User")).ToDto();
}
