using FluentAssertions;
using VehicleDebts.Domain.Exceptions;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Tests.Domain;

public class PlateTests
{
    [Theory]
    [InlineData("ABC1234")]
    [InlineData("ABC1D23")]
    public void Valid_plate_is_accepted(string input)
    {
        var act = () => new Plate(input);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("abc1234", "ABC1234")]
    [InlineData(" ABC1234 ", "ABC1234")]
    [InlineData("abc1d23", "ABC1D23")]
    public void Plate_is_normalized_to_uppercase_and_trimmed(string input, string expected)
    {
        new Plate(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("AB1234")]
    [InlineData("ABCD1234")]
    [InlineData("ABC123")]
    [InlineData("ABC12345")]
    [InlineData("ABC1234X")]
    public void Invalid_format_throws(string input)
    {
        var act = () => new Plate(input);
        act.Should().Throw<InvalidPlateException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_or_whitespace_throws(string input)
    {
        var act = () => new Plate(input);
        act.Should().Throw<InvalidPlateException>();
    }
}
