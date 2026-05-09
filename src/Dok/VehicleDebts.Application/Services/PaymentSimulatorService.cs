using System.Globalization;
using VehicleDebts.Application.DTOs;

namespace VehicleDebts.Application.Services;

public sealed class PaymentSimulatorService
{
    private static readonly int[] Installments   = [1, 6, 12];
    private const decimal MonthlyRate            = 0.025m;
    private const decimal PixDiscountRate        = 0.05m;

    public IReadOnlyList<OpcaoPagamentoDto> Simulate(IReadOnlyList<(string Tipo, decimal ValorBase)> options)
        => options.Select(o => Build(o.Tipo, o.ValorBase)).ToList();

    private static OpcaoPagamentoDto Build(string tipo, decimal valorBase)
    {
        var pix      = new PixDto(Money(Round(valorBase * (1 - PixDiscountRate))));
        var parcelas = Installments.Select(n => new ParcelaDto(n, Money(Installment(valorBase, n)))).ToList();
        return new OpcaoPagamentoDto(tipo, Money(valorBase), pix, new CartaoCreditoDto(parcelas));
    }

    private static decimal Installment(decimal principal, int n)
    {
        if (n == 1) return principal;
        var i      = MonthlyRate;
        var factor = (decimal)Math.Pow((double)(1 + i), n);
        return Round(principal * i * factor / (factor - 1));
    }

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);
}
