using VehicleDebts.Domain.Enums;
using VehicleDebts.Domain.Interfaces;

namespace VehicleDebts.Application.Services.Interest;

public sealed class MultaInterestCalculator : IInterestCalculator
{
    private const decimal DailyRate = 0.01m;

    public bool CanHandle(DebtType type) => type == DebtType.MULTA;

    public decimal Calculate(decimal originalAmount, int daysOverdue)
    {
        if (daysOverdue <= 0) return 0m;
        return originalAmount * DailyRate * daysOverdue;
    }
}
