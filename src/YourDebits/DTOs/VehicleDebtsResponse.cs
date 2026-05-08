using System.Text.Json.Serialization;

namespace YourDebits.DTOs;

public class VehicleDebtsResponse
{
    [JsonPropertyName("vehicle")]
    public string Vehicle { get; set; } = string.Empty;

    [JsonPropertyName("debts")]
    public IEnumerable<DebtItemResponse> Debts { get; set; } = [];
}