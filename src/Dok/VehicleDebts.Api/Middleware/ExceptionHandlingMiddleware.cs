using System.Text.Json;
using VehicleDebts.Application.Abstractions;
using VehicleDebts.Domain.Exceptions;

namespace VehicleDebts.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRequestLogContext logCtx)
    {
        try
        {
            await next(context);
        }
        catch (InvalidPlateException ex)
        {
            logCtx.SetError(nameof(InvalidPlateException), ex.Message);
            await WriteError(context, StatusCodes.Status400BadRequest,
                new { error = "invalid_plate" });
        }
        catch (UnknownDebtTypeException ex)
        {
            logCtx.SetError(nameof(UnknownDebtTypeException), ex.Message);
            await WriteError(context, StatusCodes.Status422UnprocessableEntity,
                new { error = "unknown_debt_type", type = ex.DebtType });
        }
        catch (AllProvidersUnavailableException ex)
        {
            logCtx.SetError(nameof(AllProvidersUnavailableException), ex.Message);
            await WriteError(context, StatusCodes.Status503ServiceUnavailable,
                new { error = "all_providers_unavailable" });
        }
        catch (Exception ex)
        {
            logCtx.SetError(ex.GetType().Name, ex.Message);
            await WriteError(context, StatusCodes.Status500InternalServerError,
                new { error = "internal_error" });
        }
    }

    private static async Task WriteError(HttpContext context, int statusCode, object body)
    {
        context.Response.StatusCode  = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}