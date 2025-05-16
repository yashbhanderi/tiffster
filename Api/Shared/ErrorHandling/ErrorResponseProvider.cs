using System.Net;

namespace Api.Shared.ErrorHandling;

public record ErrorResponse (string Code, string Message, object Data = null);

public interface IErrorResponseProvider
{
    ErrorResponseProvider GetErrorResponse(string errorCode);
}

public class ErrorResponseProvider : IErrorResponseProvider
{
    public string Code { get; set; }
    public string Message { get; set; }
    public HttpStatusCode StatusCode { get; set; }
    public string[]? ValidationErrors { get; set; }
    private static List<ErrorResponseProvider> ErrorResponses { get; } = new();
    
    public ErrorResponseProvider()
    {
    }

    public ErrorResponseProvider(string errorCode, string errorMessage, HttpStatusCode statusCode,
        string[]? validationErrors = null)
    {
        Code = errorCode;
        Message = errorMessage;
        StatusCode = statusCode;
        ValidationErrors = validationErrors;
        ErrorResponses.Add(this);
    }
    
    public ErrorResponseProvider GetErrorResponse(string errorCode)
    {
        return ErrorResponses.First(er => er.Code.ToLowerInvariant().Equals(errorCode.ToLowerInvariant()));
    }

    public static readonly ErrorResponseProvider InvalidSessionName = new("invalid_session_name", "Session name is Invalid", HttpStatusCode.BadRequest);
    public static readonly ErrorResponseProvider InvalidFileUrl = new("invalid_file_url", "File url is Invalid", HttpStatusCode.BadRequest);
    
    // Common errors
    public static readonly ErrorResponseProvider InvalidRequestParameters = new("invalid_request_parameters", "One or more parameters invalid", HttpStatusCode.BadRequest);
    public static readonly ErrorResponseProvider UnhandledException = new("internal_server_error", "Something went wrong", HttpStatusCode.InternalServerError);
}