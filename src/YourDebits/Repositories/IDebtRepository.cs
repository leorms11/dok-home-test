using YourDebits.Models;

namespace YourDebits.Repositories;

public interface IDebtRepository
{
    IEnumerable<Debt> GetByVehicle(string vehicle);
    Debt? GetById(Guid id);
    void Add(Debt debt);
    bool Delete(Guid id);
}