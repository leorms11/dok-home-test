using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Infrastructure.Configuration;
using VehicleDebts.Infrastructure.Providers.DebtsOnline;
using VehicleDebts.Infrastructure.Providers.YourDebits;
using VehicleDebts.Infrastructure.Resilience;

namespace VehicleDebts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var providers = configuration.GetSection("Providers").Get<ProvidersOptions>()
            ?? new ProvidersOptions();

        var cb = configuration.GetSection("CircuitBreaker").Get<CircuitBreakerOptions>()
            ?? new CircuitBreakerOptions();

        var cbDuration = TimeSpan.FromSeconds(cb.OpenDurationSeconds);

        ConfigureHttpClient(services, "YourDebits",  providers.YourDebits);
        ConfigureHttpClient(services, "DebtsOnline", providers.DebtsOnline);

        services.AddSingleton<YourDebitsProvider>();
        services.AddSingleton<DebtsOnlineProvider>();

        var yourDebitsCb  = new CircuitBreaker("YourDebits",  cb.FailureThreshold, cbDuration);
        var debtsOnlineCb = new CircuitBreaker("DebtsOnline", cb.FailureThreshold, cbDuration);

        // Order matters: YourDebits is tried first, DebtsOnline is the fallback
        services.AddSingleton<IDebtProvider>(sp =>
            new CircuitBreakerDebtProvider(sp.GetRequiredService<YourDebitsProvider>(), yourDebitsCb));

        services.AddSingleton<IDebtProvider>(sp =>
            new CircuitBreakerDebtProvider(sp.GetRequiredService<DebtsOnlineProvider>(), debtsOnlineCb));

        return services;
    }

    private static void ConfigureHttpClient(
        IServiceCollection services, string name, ProviderEntry entry)
    {
        services.AddHttpClient(name, client =>
        {
            client.BaseAddress = new Uri(entry.BaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(entry.TimeoutSeconds);
            client.DefaultRequestHeaders.Add("Authorization", entry.ApiKey);
        });
    }
}
