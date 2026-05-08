using YourDebits.DTOs;
using YourDebits.Repositories;
using YourDebits.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDebtRepository, DebtRepository>();
builder.Services.AddSingleton(new ApiFeatureFlag
{
    IsEnabled = builder.Configuration.GetValue("ApiEnabled", true)
});
builder.Services.AddSingleton<DelayFeatureFlag>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<YourDebits.Middlewares.FeatureFlagMiddleware>();
app.UseMiddleware<YourDebits.Middlewares.DelayMiddleware>();
app.UseMiddleware<YourDebits.Middlewares.ApiKeyMiddleware>();

app.MapGet("/", () => "Hello World");

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

app.MapGet("/api/vehicle/{vehicleId}/debts", (string vehicleId, IDebtRepository repository) =>
{
    var debts = repository.GetByVehicle(vehicleId)
        .Select(d => new DebtItemResponse
        {
            Type = d.Type.ToString(),
            Amount = d.Amount,
            DueDate = d.DueDate.ToString("yyyy-MM-dd")
        });

    return Results.Ok(new VehicleDebtsResponse
    {
        Vehicle = vehicleId,
        Debts = debts
    });
});

app.Run();