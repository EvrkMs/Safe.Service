using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;

namespace Safe.Host.Filters;

public sealed class SafeApiExceptionFilter(ILogger<SafeApiExceptionFilter> logger) : IAsyncExceptionFilter
{
    public Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;

        switch (exception)
        {
            case ValidationException validationException:
                HandleValidationException(context, validationException);
                break;
            case KeyNotFoundException notFound:
                logger.LogWarning(notFound, "Resource not found: {Message}", notFound.Message);
                context.Result = new NotFoundObjectResult(new ProblemDetails
                {
                    Title = "Not Found",
                    Detail = notFound.Message,
                    Status = StatusCodes.Status404NotFound
                });
                context.ExceptionHandled = true;
                break;
            case InvalidOperationException invalidOperation:
                logger.LogWarning(invalidOperation, "Invalid operation: {Message}", invalidOperation.Message);
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Invalid state",
                    Detail = invalidOperation.Message,
                    Status = StatusCodes.Status409Conflict
                })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                context.ExceptionHandled = true;
                break;
            case DbUpdateConcurrencyException concurrency:
                logger.LogWarning(concurrency, "Concurrency conflict detected");
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Title = "Concurrency conflict",
                    Detail = "Запись была изменена другим пользователем. Обновите данные и попробуйте снова.",
                    Status = StatusCodes.Status409Conflict
                })
                {
                    StatusCode = StatusCodes.Status409Conflict
                };
                context.ExceptionHandled = true;
                break;
        }

        return Task.CompletedTask;
    }

    private static void HandleValidationException(ExceptionContext context, ValidationException exception)
    {
        var modelState = new ModelStateDictionary();
        foreach (var error in exception.Errors)
        {
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(modelState));
        context.ExceptionHandled = true;
    }
}
