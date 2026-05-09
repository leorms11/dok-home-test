using System.Net.Http.Json;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Infrastructure.Providers.YourDebits;

internal sealed class YourDebitsProvider(IHttpClientFactory httpClientFactory) : IDebtProvider
{
    public string Name => "YourDebits";

    public async Task<IReadOnlyList<RawDebt>> GetDebtsAsync(string plate, CancellationToken ct)
    {
        var client   = httpClientFactory.CreateClient("YourDebits");
        var response = await client.GetAsync($"api/vehicle/{plate}/debts", ct);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<YourDebitsResponse>(ct)
            ?? throw new InvalidOperationException("Empty response from YourDebits");

        return result.Debts
            .Select(d => new RawDebt(
                d.Type,
                d.Amount,
                DateOnly.ParseExact(d.DueDate, "yyyy-MM-dd", null)))
            .ToList();
    }
}
