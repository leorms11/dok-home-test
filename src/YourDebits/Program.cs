using YourDebits.DTOs;
using YourDebits.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDebtRepository, DebtRepository>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<YourDebits.Middlewares.ApiKeyMiddleware>();

app.MapGet("/", () => "Hello World");

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