namespace YourDebits.Middlewares;

public class ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var expectedApiKey = config["ApiKey"];
        var providedApiKey = context.Request.Headers["Authorization"].FirstOrDefault();

        if (providedApiKey != expectedApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        await next(context);
    }
}