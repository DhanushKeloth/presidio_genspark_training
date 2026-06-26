namespace BusBookingAPI.Models.Exceptions;

/// <summary>
/// Thrown when login credentials are invalid (wrong email or password).
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid email or password.") { }

    public InvalidCredentialsException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when a user/operator/admin account is not in an active/approved state.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class AccountNotActiveException : Exception
{
    public AccountNotActiveException()
        : base("Account is not active or pending approval.") { }

    public AccountNotActiveException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when attempting to register with an email that already exists.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException()
        : base("This email address is already registered.") { }

    public EmailAlreadyRegisteredException(string message)
        : base(message) { }
}

/// <summary>
/// Thrown when password and confirm-password do not match during registration.
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class PasswordMismatchException : Exception
{
    public PasswordMismatchException()
        : base("Password and confirm password do not match.") { }

    public PasswordMismatchException(string message)
        : base(message) { }
}
