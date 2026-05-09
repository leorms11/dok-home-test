using System.Diagnostics;
using System.Text.RegularExpressions;
using VehicleDebts.Application.Abstractions;

namespace VehicleDebts.Api.Middleware;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    // matches old (ABC1234) and Mercosul (ABC1D23) plates inside the path
    private static readonly Regex PlateInPath =
        new(@"(?<=/vehicle/)[A-Z]{3}[A-Z0-9]{4}(?=/|$)", RegexOptions.Compiled);

    public async Task InvokeAsync(HttpContext context, IRequestLogContext logCtx)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            sw.Stop();
            logCtx.SetDuration(sw.ElapsedMilliseconds);
            var snapshot   = logCtx.GetSnapshot();
            var statusCode = context.Response.StatusCode;
            var path       = MaskPath(context.Request.Path.Value ?? string.Empty);

            if (snapshot.Error is not null || statusCode >= 500)
                logger.LogError(
                    "HTTP {Method} {Path} {StatusCode} {DurationMs}ms | Plate={Plate} | {@Providers} | {@Debts} | {@Error}",
                    context.Request.Method, path, statusCode, snapshot.DurationMs,
                    snapshot.Plate, snapshot.Providers, snapshot.Debts, snapshot.Error);
            else
                logger.LogInformation(
                    "HTTP {Method} {Path} {StatusCode} {DurationMs}ms | Plate={Plate} | {@Providers} | {@Debts}",
                    context.Request.Method, path, statusCode, snapshot.DurationMs,
                    snapshot.Plate, snapshot.Providers, snapshot.Debts);
        }
    }

    private static string MaskPath(string path) =>
        PlateInPath.Replace(path, m => m.Value[..3] + "****");
}