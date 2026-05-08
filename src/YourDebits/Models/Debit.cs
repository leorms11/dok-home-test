using System.Text.Json.Serialization;
using YourDebits.Converters;
using YourDebits.Enums;

namespace YourDebits.Models;

public class Debit
{
    public Guid Id { get; set; }
    public DebitType Type { get; set; }
    public decimal Amount { get; set; }
    public string Vehicle { get; set; } = string.Empty;

    [JsonConverter(typeof(DateTimeFormatConverter))]
    public DateTime DueDate { get; set; }
}