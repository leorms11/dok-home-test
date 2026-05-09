namespace VehicleDebts.Domain.ValueObjects;

public record RawDebt(string Type, decimal Amount, DateOnly DueDate);
