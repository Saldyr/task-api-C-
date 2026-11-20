namespace TasksApi.Errors;

// ≈ NestJS HttpException: a business error that carries its HTTP status code.
// Services throw it; ApiExceptionHandler turns it into a ProblemDetails response.
public class ApiException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;

    public string Title { get; } = title;
}
