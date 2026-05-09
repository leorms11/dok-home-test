using System.Globalization;
using FluentAssertions;
using VehicleDebts.Application.DTOs;
using VehicleDebts.Application.Services;

namespace VehicleDebts.Tests.Application;

public class PaymentSimulatorServiceTests
{
    private readonly PaymentSimulatorService _sut = new();

    private OpcaoPagamentoDto Single(decimal valorBase) =>
        _sut.Simulate([("TOTAL", valorBase)])[0];

    // PIX

    [Theory]
    [InlineData(2355.93, "2238.13")]
    [InlineData(1800.00, "1710.00")]
    [InlineData( 555.93,  "528.13")]
    [InlineData(   0.01,    "0.01")]
    [InlineData(   1.00,    "0.95")]
    public void Pix_applies_5_percent_discount(decimal valorBase, string expected)
    {
        Single(valorBase).Pix.TotalComDesconto.Should().Be(expected);
    }

    // Cartão 1x

    [Theory]
    [InlineData(2355.93)]
    [InlineData(1800.00)]
    [InlineData( 555.93)]
    public void Credit_card_1x_equals_base_value(decimal valorBase)
    {
        var parcela1x = Single(valorBase).CartaoCredito.Parcelas.Single(p => p.Quantidade == 1);
        parcela1x.ValorParcela.Should().Be(valorBase.ToString("F2", CultureInfo.InvariantCulture));
    }

    // PMT 6x e 12x

    [Theory]
    [InlineData(2355.93,  6, "427.72")]
    [InlineData(2355.93, 12, "229.67")]
    [InlineData(1800.00,  6, "326.79")]
    [InlineData(1800.00, 12, "175.48")]
    [InlineData( 555.93,  6, "100.93")]
    [InlineData( 555.93, 12,  "54.20")]
    public void Credit_card_installments_use_Price_formula(decimal valorBase, int n, string expected)
    {
        var parcela = Single(valorBase).CartaoCredito.Parcelas.Single(p => p.Quantidade == n);
        var actual   = decimal.Parse(parcela.ValorParcela, CultureInfo.InvariantCulture);
        var exp      = decimal.Parse(expected, CultureInfo.InvariantCulture);
        actual.Should().BeApproximately(exp, 0.02m);
    }

    // Estrutura

    [Fact]
    public void Each_option_has_exactly_3_installments_with_quantities_1_6_12()
    {
        var opcao = Single(1000m);
        opcao.CartaoCredito.Parcelas.Should().HaveCount(3);
        opcao.CartaoCredito.Parcelas.Select(p => p.Quantidade)
            .Should().BeEquivalentTo([1, 6, 12]);
    }

    [Fact]
    public void No_installment_quantity_other_than_1_6_12_appears()
    {
        var opcao = Single(999m);
        opcao.CartaoCredito.Parcelas
            .Select(p => p.Quantidade)
            .Should().OnlyContain(q => q == 1 || q == 6 || q == 12);
    }
}
