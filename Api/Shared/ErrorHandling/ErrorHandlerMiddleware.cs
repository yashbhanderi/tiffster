using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;

namespace Api.Shared.ErrorHandling;

public class ErrorHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IErrorResponseProvider _errorResponsesProvider;

    public ErrorHandlerMiddleware(RequestDelegate next, IErrorResponseProvider errorResponsesProvider)
    {
        _next = next;
        _errorResponsesProvider = errorResponsesProvider;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception error)
        {
            var response = context.Response;
            response.ContentType = "application/json";
            ErrorResponseProvider errorResponse = null;
            object errorData = null;
            switch (error)
            {
                case ValidationFailureException v:
                    errorResponse = _errorResponsesProvider.GetErrorResponse(v.Failures.First().ErrorCode) ?? ErrorResponseProvider.UnhandledException;
                    response.StatusCode = (int)errorResponse.StatusCode;
                    break;

                case JsonException:
                case BadHttpRequestException:
                    // custom application error
                    errorResponse = ErrorResponseProvider.InvalidRequestParameters;
                    response.StatusCode = (int)ErrorResponseProvider.InvalidRequestParameters.StatusCode;
                    break;

                default:
                    // server error
                    errorResponse = ErrorResponseProvider.UnhandledException;
                    response.StatusCode = (int)errorResponse.StatusCode;
                    break;
            }

            var result = string.Empty;
            if (!errorResponse.Code.IsEmpty() || !errorResponse.Message.IsEmpty() || errorData != null)
            {
                result = JsonSerializer.Serialize(
                    new ErrorResponse(errorResponse.Code, errorResponse.Message, errorData),
                    new JsonSerializerOptions
                    {
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        PropertyNamingPolicy = new JsonSnakeCaseNamingPolicy()
                    });
            }

            await response.WriteAsync(result);
        }
    }
}