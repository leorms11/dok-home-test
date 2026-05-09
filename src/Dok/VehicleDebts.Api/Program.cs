using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Text.Json.Serialization;
using VehicleDebts.Api.Middleware;
using VehicleDebts.Application;
using VehicleDebts.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .Enrich.With(new RemovePropertiesEnricher("RequestPath"))
       .WriteTo.Console(new JsonFormatter()));

builder.WebHost.ConfigureKestrel(opts =>
    opts.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }

internal sealed class RemovePropertiesEnricher(params string[] names) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory _)
    {
        foreach (var name in names)
            logEvent.RemovePropertyIfPresent(name);
    }
}