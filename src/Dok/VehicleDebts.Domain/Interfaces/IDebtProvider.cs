using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Domain.Interfaces;

public interface IDebtProvider
{
    string Name { get; }
    Task<IReadOnlyList<RawDebt>> GetDebtsAsync(string plate, CancellationToken ct);
}
