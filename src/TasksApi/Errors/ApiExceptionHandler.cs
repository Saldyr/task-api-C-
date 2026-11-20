using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace TasksApi.Errors;

// ≈ a global NestJS ExceptionFilter. Called by app.UseExceptionHandler() for any
// unhandled exception. Returning false falls back to the default behavior (500 ProblemDetails).
public class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiException)
        {
            return false;
        }

        httpContext.Response.StatusCode = apiException.StatusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = apiException.StatusCode,
                Title = apiException.Title,
                Detail = apiException.Message,
            },
        });
    }
}
