using System.Text.Json.Serialization;
using YourDebits.Converters;
using YourDebits.Enums;

namespace YourDebits.Models;

public class Debt
{
    public Guid Id { get; set; }
    public DebtType Type { get; set; }
    public decimal Amount { get; set; }
    public string Vehicle { get; set; } = string.Empty;

    [JsonConverter(typeof(DateTimeFormatConverter))]
    public DateTime DueDate { get; set; }
}