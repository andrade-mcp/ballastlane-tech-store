namespace BallastlaneTechStore.Application.Common;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string what) : base($"{what} not found.") { }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public sealed class AuthenticationException : Exception
{
    public AuthenticationException(string message) : base(message) { }
}

public sealed class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
