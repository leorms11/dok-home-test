namespace VehicleDebts.Infrastructure.Configuration;

public sealed class ProvidersOptions
{
    public ProviderEntry YourDebits  { get; set; } = new();
    public ProviderEntry DebtsOnline { get; set; } = new();
}

public sealed class ProviderEntry
{
    public string BaseUrl       { get; set; } = string.Empty;
    public int    TimeoutSeconds { get; set; } = 5;
    public string ApiKey        { get; set; } = string.Empty;
}
