using DebtsOnline.Enums;
using DebtsOnline.Models;

namespace DebtsOnline.Repositories;

public class DebtRepository : IDebtRepository
{
    private readonly List<Debt> _debts =
    [
        new() { Id = Guid.NewGuid(), Plate = "ABC1234", Category = DebtCategory.IPVA,          Value = 1500.00m, Expiration = new DateTime(2024, 1, 10) },
        new() { Id = Guid.NewGuid(), Plate = "ABC1234", Category = DebtCategory.MULTA,         Value =  300.50m, Expiration = new DateTime(2024, 2, 15) },
    ];

    public IEnumerable<Debt> GetByPlate(string plate) =>
        _debts.Where(d => d.Plate.Equals(plate, StringComparison.OrdinalIgnoreCase));
}