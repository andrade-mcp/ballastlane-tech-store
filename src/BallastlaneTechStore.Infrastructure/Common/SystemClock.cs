using BallastlaneTechStore.Application.Abstractions;

namespace BallastlaneTechStore.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
