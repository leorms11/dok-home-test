using VehicleDebts.Domain.Enums;
using VehicleDebts.Domain.Interfaces;

namespace VehicleDebts.Application.Services.Interest;

public sealed class IpvaInterestCalculator : IInterestCalculator
{
    private const decimal DailyRate = 0.0033m;
    private const decimal Cap       = 0.20m;

    public bool CanHandle(DebtType type) => type == DebtType.IPVA;

    public decimal Calculate(decimal originalAmount, int daysOverdue)
    {
        if (daysOverdue <= 0) return 0m;
        var interest = originalAmount * DailyRate * daysOverdue;
        var cap      = originalAmount * Cap;
        return Math.Min(interest, cap);
    }
}
