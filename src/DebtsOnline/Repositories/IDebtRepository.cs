using DebtsOnline.Models;

namespace DebtsOnline.Repositories;

public interface IDebtRepository
{
    IEnumerable<Debt> GetByPlate(string plate);
}