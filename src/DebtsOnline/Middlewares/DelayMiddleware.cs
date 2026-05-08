using DebtsOnline.Services;

namespace DebtsOnline.Middlewares;

public class DelayMiddleware(RequestDelegate next, DelayFeatureFlag featureFlag)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (featureFlag.IsEnabled && featureFlag.DelayMs > 0
            && !context.Request.Path.StartsWithSegments("/api/feature-flag"))
            await Task.Delay(featureFlag.DelayMs);

        await next(context);
    }
}