using VehicleDebts.Application.Logging;

namespace VehicleDebts.Application.Abstractions;

public interface IRequestLogContext
{
    void SetPlate(string plate);
    void AddProviderAttempt(string name, bool success, long durationMs, string healthState, Exception? error = null);
    void SetDebtsResult(int count, IEnumerable<string> types, decimal totalOriginal, decimal totalUpdated);
    void SetError(string type, string message);
    void SetDuration(long durationMs);
    RequestLogSnapshot GetSnapshot();
}