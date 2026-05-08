using BallastlaneTechStore.Domain.Common;
using BallastlaneTechStore.Domain.Enums;

namespace BallastlaneTechStore.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = default!;
    public string PasswordHash { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public UserRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User() { }

    public static User Create(string email, string passwordHash, string displayName, UserRole role, DateTime nowUtc)
    {
        var normalised = NormaliseEmail(email);
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new DomainException("Password hash is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new DomainException("Display name is required.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = normalised,
            PasswordHash = passwordHash,
            DisplayName = displayName.Trim(),
            Role = role,
            CreatedAt = nowUtc,
        };
    }

    public static User Hydrate(Guid id, string email, string passwordHash, string displayName, UserRole role, DateTime createdAt)
        => new() { Id = id, Email = email, PasswordHash = passwordHash, DisplayName = displayName, Role = role, CreatedAt = createdAt };

    public static string NormaliseEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new DomainException("Email is required.");
        var trimmed = email.Trim().ToLowerInvariant();
        if (!trimmed.Contains('@') || trimmed.Length < 3) throw new DomainException("Email is not valid.");
        return trimmed;
    }
}
