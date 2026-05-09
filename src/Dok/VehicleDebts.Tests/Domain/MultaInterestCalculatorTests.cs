using FluentAssertions;
using VehicleDebts.Application.Services.Interest;
using VehicleDebts.Domain.Enums;

namespace VehicleDebts.Tests.Domain;

public class MultaInterestCalculatorTests
{
    private readonly MultaInterestCalculator _sut = new();

    [Fact]
    public void CanHandle_returns_true_for_MULTA() =>
        _sut.CanHandle(DebtType.MULTA).Should().BeTrue();

    [Fact]
    public void CanHandle_returns_false_for_IPVA() =>
        _sut.CanHandle(DebtType.IPVA).Should().BeFalse();

    [Theory]
    [InlineData(300.50,  85, 255.425)] // sem arredondamento — feito no handler
    [InlineData(300.50,   0,   0.00)]
    [InlineData(300.50,  -1,   0.00)]
    [InlineData(100.00,   1,   1.00)]
    [InlineData(100.00, 200, 200.00)]  // sem teto
    public void Calculate_returns_correct_interest(decimal amount, int days, decimal expected)
    {
        _sut.Calculate(amount, days).Should().Be(expected);
    }
}
