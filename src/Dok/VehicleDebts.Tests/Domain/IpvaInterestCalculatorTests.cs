using FluentAssertions;
using VehicleDebts.Application.Services.Interest;
using VehicleDebts.Domain.Enums;

namespace VehicleDebts.Tests.Domain;

public class IpvaInterestCalculatorTests
{
    private readonly IpvaInterestCalculator _sut = new();

    [Fact]
    public void CanHandle_returns_true_for_IPVA() =>
        _sut.CanHandle(DebtType.IPVA).Should().BeTrue();

    [Fact]
    public void CanHandle_returns_false_for_MULTA() =>
        _sut.CanHandle(DebtType.MULTA).Should().BeFalse();

    [Theory]
    [InlineData(1500.00, 121, 300.00)]  // teto ativado: min(598.95, 300.00)
    [InlineData(1500.00,   1,   4.95)]  // sem teto: min(4.95, 300.00)
    [InlineData(1500.00,  60, 297.00)]  // logo abaixo do teto
    [InlineData(1500.00,  61, 300.00)]  // teto ativado exatamente: min(301.95, 300.00)
    [InlineData(1000.00,   0,   0.00)]  // não vencido
    [InlineData(1000.00,  -5,   0.00)]  // dias negativos
    public void Calculate_returns_correct_interest(decimal amount, int days, decimal expected)
    {
        _sut.Calculate(amount, days).Should().Be(expected);
    }
}
