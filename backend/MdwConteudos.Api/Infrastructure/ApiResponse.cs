namespace MdwConteudos.Api.Infrastructure;

public record ApiSuccessResponse<T>(bool Success, T Data, int Total, string Timestamp);
public record ApiSuccessResponse(bool Success, string Timestamp);
public record ApiErrorResponse(bool Success, string Error, string? Message = null, string? Timestamp = null);

public static class ApiResponse
{
    public static object Ok<T>(T data, int? total = null) => new
    {
        success = true,
        data,
        total = total ?? (data is System.Collections.ICollection c ? c.Count : 1),
        timestamp = DateTime.Now.ToString("o")
    };

    public static object OkMessage(string message) => new
    {
        success = true,
        message,
        timestamp = DateTime.Now.ToString("o")
    };

    public static object Fail(string error, string? message = null, int status = 400) =>
        new ApiErrorResponse(false, error, message, DateTime.Now.ToString("o"));
}
