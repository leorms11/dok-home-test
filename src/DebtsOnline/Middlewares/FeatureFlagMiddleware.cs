using DebtsOnline.Services;

namespace DebtsOnline.Middlewares;

public class FeatureFlagMiddleware(RequestDelegate next, ApiFeatureFlag featureFlag)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!featureFlag.IsEnabled && !context.Request.Path.StartsWithSegments("/api/feature-flag"))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        await next(context);
    }
}