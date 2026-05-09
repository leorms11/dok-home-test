namespace VehicleDebts.Infrastructure.Configuration;

public sealed class CircuitBreakerOptions
{
    public int FailureThreshold    { get; set; } = 3;
    public int OpenDurationSeconds { get; set; } = 30;
}
