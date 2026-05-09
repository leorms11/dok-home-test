namespace VehicleDebts.Domain.Exceptions;

public sealed class AllProvidersUnavailableException()
    : Exception("All debt providers are unavailable");
