using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Infrastructure.Resilience;

internal sealed class CircuitBreakerDebtProvider(IDebtProvider inner, CircuitBreaker breaker)
    : IDebtProvider, IProviderHealthInfo
{
    public string Name        => inner.Name;
    public string HealthState => breaker.State.ToString();

    public Task<IReadOnlyList<RawDebt>> GetDebtsAsync(string plate, CancellationToken ct)
        => breaker.ExecuteAsync(token => inner.GetDebtsAsync(plate, token), ct);
}
