namespace KiaraFin.Api.Common.Exceptions;

public abstract class AppException(string message) : Exception(message);

public sealed class AppValidationException(string message) : AppException(message);
public sealed class ConflictException(string message) : AppException(message);
public sealed class ForbiddenException(string message) : AppException(message);
public sealed class NotFoundException(string message) : AppException(message);
public sealed class AuthenticationException(string message) : AppException(message);
