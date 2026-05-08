using DebtsOnline.Middlewares;
using DebtsOnline.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<IDebtRepository, DebtRepository>();

var app = builder.Build();

app.UseMiddleware<AuthorizationMiddleware>();
app.MapControllers();

app.Run();