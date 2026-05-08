using System.Text.Json.Serialization;

namespace YourDebits.DTOs;

public class VehicleDebitsResponse
{
    [JsonPropertyName("vehicle")]
    public string Vehicle { get; set; } = string.Empty;

    [JsonPropertyName("debts")]
    public IEnumerable<DebitItemResponse> Debts { get; set; } = [];
}