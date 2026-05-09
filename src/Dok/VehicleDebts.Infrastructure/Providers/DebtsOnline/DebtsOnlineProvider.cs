using System.Globalization;
using System.Xml.Linq;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Infrastructure.Providers.DebtsOnline;

internal sealed class DebtsOnlineProvider(IHttpClientFactory httpClientFactory) : IDebtProvider
{
    public string Name => "DebtsOnline";

    public async Task<IReadOnlyList<RawDebt>> GetDebtsAsync(string plate, CancellationToken ct)
    {
        var client   = httpClientFactory.CreateClient("DebtsOnline");
        var response = await client.GetAsync($"api/plate/{plate}/debts", ct);
        response.EnsureSuccessStatusCode();

        var xml           = await response.Content.ReadAsStringAsync(ct);
        var doc           = XDocument.Parse(xml);
        var debtsElement  = doc.Root?.Element("debts");

        if (debtsElement is null || !debtsElement.HasElements)
            return [];

        return debtsElement.Elements("debt")
            .Select(d => new RawDebt(
                d.Element("category")!.Value,
                decimal.Parse(d.Element("value")!.Value, CultureInfo.InvariantCulture),
                DateOnly.ParseExact(d.Element("expiration")!.Value, "yyyy-MM-dd", null)))
            .ToList();
    }
}
