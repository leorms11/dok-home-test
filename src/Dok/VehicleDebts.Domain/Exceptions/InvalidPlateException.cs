namespace VehicleDebts.Domain.Exceptions;

public sealed class InvalidPlateException(string plate)
    : Exception($"Invalid plate format: '{plate}'");
