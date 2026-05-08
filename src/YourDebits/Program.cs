using YourDebits.DTOs;
using YourDebits.Repositories;
using YourDebits.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDebtRepository, DebtRepository>();
builder.Services.AddSingleton(new ApiFeatureFlag
{
    IsEnabled = builder.Configuration.GetValue("ApiEnabled", true)
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<YourDebits.Middlewares.FeatureFlagMiddleware>();
app.UseMiddleware<YourDebits.Middlewares.ApiKeyMiddleware>();

app.MapGet("/", () => "Hello World");

app.MapPost("/api/feature-flag/toggle", (ApiFeatureFlag featureFlag) =>
{
    featureFlag.IsEnabled = !featureFlag.IsEnabled;
    return Results.Ok(new { apiEnabled = featureFlag.IsEnabled });
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