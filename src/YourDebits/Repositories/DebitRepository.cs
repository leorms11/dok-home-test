using YourDebits.Enums;
using YourDebits.Models;

namespace YourDebits.Repositories;

public class DebitRepository : IDebitRepository
{
    private readonly List<Debit> _debits =
    [
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebitType.IPVA,          Amount = 1500.00m, DueDate = new DateTime(2024, 1, 10) },
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebitType.MULTA,         Amount =  300.50m, DueDate = new DateTime(2024, 2, 15) },
        new() { Id = Guid.NewGuid(), Vehicle = "ABC1234", Type = DebitType.LICENCIAMENTO, Amount = 1000.00m, DueDate = new DateTime(2024, 3, 20) },
    ];

    public IEnumerable<Debit> GetByVehicle(string vehicle) =>
        _debits.Where(d => d.Vehicle.Equals(vehicle, StringComparison.OrdinalIgnoreCase));

    public Debit? GetById(Guid id) =>
        _debits.FirstOrDefault(d => d.Id == id);

    public void Add(Debit debit) => _debits.Add(debit);

    public bool Delete(Guid id)
    {
        var debit = GetById(id);
        if (debit is null) return false;
        _debits.Remove(debit);
        return true;
    }
}