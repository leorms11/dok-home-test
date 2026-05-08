using YourDebits.Models;

namespace YourDebits.Repositories;

public interface IDebitRepository
{
    IEnumerable<Debit> GetByVehicle(string vehicle);
    Debit? GetById(Guid id);
    void Add(Debit debit);
    bool Delete(Guid id);
}