using VehicleDebts.Application.Abstractions;

namespace VehicleDebts.Application.Logging;

public sealed record RequestLogSnapshot(
    string? Plate,
    IReadOnlyList<ProviderAttemptSnapshot> Providers,
    DebtsSnapshot? Debts,
    ErrorSnapshot? Error,
    long DurationMs);

public sealed record ProviderAttemptSnapshot(
    string Name,
    bool Success,
    long DurationMs,
    string HealthState,
    ProviderErrorSnapshot? Error);

public sealed record ProviderErrorSnapshot(string Type, string Message);

public sealed record DebtsSnapshot(
    int Count,
    IReadOnlyList<string> Types,
    decimal TotalOriginal,
    decimal TotalUpdated);

public sealed record ErrorSnapshot(
    string Type,
    string Message);

internal sealed class RequestLogContext : IRequestLogContext
{
    private string? _plate;
    private readonly List<ProviderAttemptSnapshot> _providers = [];
    private DebtsSnapshot? _debts;
    private ErrorSnapshot? _error;
    private long _durationMs;

    public void SetPlate(string plate) => _plate = plate[..3] + new string('*', plate.Length - 3);

    public void AddProviderAttempt(string name, bool success, long durationMs, string healthState, Exception? error = null) =>
        _providers.Add(new ProviderAttemptSnapshot(
            name, success, durationMs, healthState,
            error is null ? null : new ProviderErrorSnapshot(error.GetType().Name, error.Message)));

    public void SetDebtsResult(int count, IEnumerable<string> types, decimal totalOriginal, decimal totalUpdated) =>
        _debts = new DebtsSnapshot(count, types.ToList(), totalOriginal, totalUpdated);

    public void SetError(string type, string message) =>
        _error = new ErrorSnapshot(type, message);

    public void SetDuration(long durationMs) => _durationMs = durationMs;

    public RequestLogSnapshot GetSnapshot() =>
        new(_plate, _providers.AsReadOnly(), _debts, _error, _durationMs);
}