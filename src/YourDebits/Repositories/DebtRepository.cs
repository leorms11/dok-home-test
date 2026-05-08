using YourDebits.Enums;
using YourDebits.Models;

namespace YourDebits.Repositories;

public class DebtRepository : IDebtRepository
{
    private readonly List<Debt> _debts =
    [
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebtType.IPVA,          Amount = 1500.00m, DueDate = new DateTime(2024, 1, 10) },
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebtType.MULTA,         Amount =  300.50m, DueDate = new DateTime(2024, 2, 15) },
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebtType.LICENCIAMENTO, Amount = 1000.00m, DueDate = new DateTime(2024, 3, 20) },
    ];

    public IEnumerable<Debt> GetByVehicle(string vehicle) =>
        _debts.Where(d => d.Vehicle.Equals(vehicle, StringComparison.OrdinalIgnoreCase));

    public Debt? GetById(Guid id) =>
        _debts.FirstOrDefault(d => d.Id == id);

    public void Add(Debt debt) => _debts.Add(debt);

    public bool Delete(Guid id)
    {
        var debt = GetById(id);
        if (debt is null) return false;
        _debts.Remove(debt);
        return true;
    }
}