namespace VehicleDebts.Infrastructure.Resilience;

internal sealed class CircuitBreaker(string name, int failureThreshold, TimeSpan openDuration)
{
    private readonly object _lock = new();
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTime _openedAt;

    public CircuitBreakerState State { get { lock (_lock) return _state; } }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        lock (_lock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                if (DateTime.UtcNow - _openedAt >= openDuration)
                    _state = CircuitBreakerState.HalfOpen;
                else
                    throw new CircuitBreakerOpenException(name);
            }
        }

        try
        {
            var result = await action(ct);
            OnSuccess();
            return result;
        }
        catch (CircuitBreakerOpenException)
        {
            throw;
        }
        catch
        {
            OnFailure();
            throw;
        }
    }

    private void OnSuccess()
    {
        lock (_lock)
        {
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
        }
    }

    private void OnFailure()
    {
        lock (_lock)
        {
            _failureCount++;
            if (_state == CircuitBreakerState.HalfOpen || _failureCount >= failureThreshold)
            {
                _state    = CircuitBreakerState.Open;
                _openedAt = DateTime.UtcNow;
            }
        }
    }
}
