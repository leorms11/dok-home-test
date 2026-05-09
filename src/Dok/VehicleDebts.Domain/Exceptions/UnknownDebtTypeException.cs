namespace VehicleDebts.Domain.Exceptions;

public sealed class UnknownDebtTypeException : Exception
{
    public string DebtType { get; }

    public UnknownDebtTypeException(string type)
        : base($"Unknown debt type: '{type}'")
    {
        DebtType = type;
    }
}
