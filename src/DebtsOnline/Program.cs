using DebtsOnline.Middlewares;
using DebtsOnline.Repositories;
using DebtsOnline.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IDebtRepository, DebtRepository>();
builder.Services.AddSingleton(new ApiFeatureFlag
{
    IsEnabled = builder.Configuration.GetValue("ApiEnabled", true)
});
builder.Services.AddSingleton<DelayFeatureFlag>();

var app = builder.Build();

app.UseMiddleware<FeatureFlagMiddleware>();
app.UseMiddleware<DelayMiddleware>();
app.UseMiddleware<AuthorizationMiddleware>();

app.MapPost("/api/feature-flag/toggle/{featureFlagName}", (string featureFlagName, ApiFeatureFlag apiFlag, DelayFeatureFlag delayFlag) =>
{
    return featureFlagName.ToLower() switch
    {
        "api"   => Results.Ok(new { name = "api",   isEnabled = (apiFlag.IsEnabled   = !apiFlag.IsEnabled) }),
        "delay" => Results.Ok(new { name = "delay", isEnabled = (delayFlag.IsEnabled = !delayFlag.IsEnabled) }),
        _       => Results.NotFound(new { error = $"Feature flag '{featureFlagName}' not found." })
    };
});

app.MapPatch("/api/feature-flag/delay", (int delayMs, DelayFeatureFlag featureFlag) =>
{
    featureFlag.DelayMs = delayMs;
    return Results.Ok(new { delayEnabled = featureFlag.IsEnabled, delayMs = featureFlag.DelayMs });
});

app.MapControllers();

app.Run();