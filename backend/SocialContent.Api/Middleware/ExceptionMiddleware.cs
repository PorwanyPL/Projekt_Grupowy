using Microsoft.AspNetCore.Mvc;
using SocialContent.Api.Domain;

namespace SocialContent.Api.Middleware;

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainValidationException ex)
        {
            await Write(context, StatusCodes.Status400BadRequest, "Błąd walidacji", ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            await Write(context, StatusCodes.Status401Unauthorized, "Brak autoryzacji", ex.Message);
        }
        catch (NotFoundException ex)
        {
            await Write(context, StatusCodes.Status404NotFound, "Nie znaleziono", ex.Message);
        }
        catch (ConflictException ex)
        {
            await Write(context, StatusCodes.Status409Conflict, "Konflikt", ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Klient zerwał połączenie - nie ma komu odpowiadać i nie jest to błąd serwera.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Nieobsłużony wyjątek");
            await Write(context, StatusCodes.Status500InternalServerError, "Błąd serwera", "Wystąpił nieoczekiwany błąd.");
        }
    }

    private static Task Write(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        });
    }
}
