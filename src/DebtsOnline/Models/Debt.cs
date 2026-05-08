using System.Text.Json.Serialization;
using DebtsOnline.Converters;
using DebtsOnline.Enums;

namespace DebtsOnline.Models;

public class Debt
{
    public Guid Id { get; set; }
    public DebtCategory Category { get; set; }
    public decimal Value { get; set; }
    public string Plate { get; set; } = string.Empty;

    [JsonConverter(typeof(DateTimeFormatConverter))]
    public DateTime Expiration { get; set; }
}