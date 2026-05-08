using System.Text.Json.Serialization;

namespace YourDebits.DTOs;

public class DebitItemResponse
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("due_date")]
    public string DueDate { get; set; } = string.Empty;
}