using VehicleDebts.Domain.Enums;

namespace VehicleDebts.Domain.Interfaces;

public interface IInterestCalculator
{
    bool CanHandle(DebtType type);
    decimal Calculate(decimal originalAmount, int daysOverdue);
}
