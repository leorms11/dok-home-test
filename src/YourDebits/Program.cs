using YourDebits.DTOs;
using YourDebits.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDebitRepository, DebitRepository>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseMiddleware<YourDebits.Middlewares.ApiKeyMiddleware>();

app.MapGet("/", () => "Hello World");

app.MapGet("/api/vehicle/{vehicleId}/debits", (string vehicleId, IDebitRepository repository) =>
{
    var debits = repository.GetByVehicle(vehicleId)
        .Select(d => new DebitItemResponse
        {
            Type = d.Type.ToString(),
            Amount = d.Amount,
            DueDate = d.DueDate.ToString("yyyy-MM-dd")
        });

    return Results.Ok(new VehicleDebitsResponse
    {
        Vehicle = vehicleId,
        Debts = debits
    });
});

app.Run();