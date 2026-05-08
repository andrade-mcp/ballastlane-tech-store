using BallastlaneTechStore.Domain.Entities;

namespace BallastlaneTechStore.Application.Abstractions;

public interface IJwtTokenIssuer
{
    (string Token, DateTime ExpiresAt) Issue(User user);
}
