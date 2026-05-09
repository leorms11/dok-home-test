using System.Text.RegularExpressions;
using VehicleDebts.Domain.Exceptions;

namespace VehicleDebts.Domain.ValueObjects;

public sealed class Plate
{
    private static readonly Regex OldPattern     = new(@"^[A-Z]{3}[0-9]{4}$",           RegexOptions.Compiled);
    private static readonly Regex MercosulPattern = new(@"^[A-Z]{3}[0-9][A-Z][0-9]{2}$", RegexOptions.Compiled);

    public string Value { get; }

    public Plate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new InvalidPlateException(input ?? string.Empty);

        var normalized = input.Trim().ToUpperInvariant();

        if (!OldPattern.IsMatch(normalized) && !MercosulPattern.IsMatch(normalized))
            throw new InvalidPlateException(normalized);

        Value = normalized;
    }

    public override string ToString() => Value;
}
