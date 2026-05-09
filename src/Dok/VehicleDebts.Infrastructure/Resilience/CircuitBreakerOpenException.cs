namespace VehicleDebts.Infrastructure.Resilience;

public sealed class CircuitBreakerOpenException(string providerName)
    : Exception($"Circuit breaker is open for provider '{providerName}'");
