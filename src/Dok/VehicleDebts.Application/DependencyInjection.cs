using Microsoft.Extensions.DependencyInjection;
using VehicleDebts.Application.Abstractions;
using VehicleDebts.Application.DTOs;
using VehicleDebts.Application.Logging;
using VehicleDebts.Application.Services;
using VehicleDebts.Application.Services.Interest;
using VehicleDebts.Application.UseCases.GetVehicleDebts;
using VehicleDebts.Domain.Interfaces;

namespace VehicleDebts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IInterestCalculator, IpvaInterestCalculator>();
        services.AddSingleton<IInterestCalculator, MultaInterestCalculator>();
        services.AddSingleton<PaymentSimulatorService>();
        services.AddScoped<IRequestLogContext, RequestLogContext>();
        services.AddScoped<IQueryHandler<GetVehicleDebtsQuery, VehicleDebtsResponse>, GetVehicleDebtsHandler>();
        return services;
    }
}
