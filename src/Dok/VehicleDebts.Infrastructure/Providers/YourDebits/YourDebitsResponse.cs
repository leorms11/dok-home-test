using System.Text.Json.Serialization;

namespace VehicleDebts.Infrastructure.Providers.YourDebits;

internal sealed class YourDebitsResponse
{
    [JsonPropertyName("vehicle")]
    public string Vehicle { get; set; } = string.Empty;

    [JsonPropertyName("debts")]
    public List<YourDebitsDebtItem> Debts { get; set; } = [];
}

internal sealed class YourDebitsDebtItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("due_date")]
    public string DueDate { get; set; } = string.Empty;
}
