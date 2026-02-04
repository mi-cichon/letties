namespace WebGame.Domain.Common;

public record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error InvalidOperation = new("Error.InvalidOperation", "The operation is invalid.");
    public static readonly Error NotFound = new("Error.NotFound", "The specified resource was not found.");
    public static readonly Error AlreadyExists = new("Error.AlreadyExists", "The specified resource already exists.");
    public static readonly Error Unauthorized = new("Error.Unauthorized", "The user is not authorized to perform this action.");
    public static readonly Error Forbidden = new("Error.Forbidden", "The user is forbidden from performing this action.");
    public static readonly Error Timeout = new("Error.Timeout", "The operation timed out.");
    public static readonly Error InvalidArgument = new("Error.InvalidArgument", "The specified argument is invalid.");
    public static readonly Error InvalidState = new("Error.InvalidState", "The operation is not valid in the current state.");
    public static readonly Error InternalError = new("Error.InternalError", "An internal error occurred.");
    public static readonly Error ExternalError = new("Error.ExternalError", "An external error occurred.");
    public static readonly Error NullValue = new("Error.NullValue", "The specified result value is null.");
    
    public static implicit operator string(Error error) => error.Code;
}