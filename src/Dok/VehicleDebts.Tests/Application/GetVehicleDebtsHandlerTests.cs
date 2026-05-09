using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleDebts.Application.Abstractions;
using VehicleDebts.Application.Services;
using VehicleDebts.Application.Services.Interest;
using VehicleDebts.Application.UseCases.GetVehicleDebts;
using VehicleDebts.Domain.Exceptions;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Tests.Application;

public class GetVehicleDebtsHandlerTests
{
    private static GetVehicleDebtsHandler BuildHandler(params IDebtProvider[] providers) =>
        new(providers,
            [new IpvaInterestCalculator(), new MultaInterestCalculator()],
            new PaymentSimulatorService(),
            Substitute.For<IRequestLogContext>());

    private static RawDebt Ipva(decimal amount = 1500m, string due = "2024-01-10") =>
        new("IPVA", amount, DateOnly.ParseExact(due, "yyyy-MM-dd", null));

    private static RawDebt Multa(decimal amount = 300.50m, string due = "2024-02-15") =>
        new("MULTA", amount, DateOnly.ParseExact(due, "yyyy-MM-dd", null));

    // Happy path

    [Fact]
    public async Task Full_spec_example_matches_expected_values()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync("ABC1234", Arg.Any<CancellationToken>())
            .Returns([Ipva(), Multa()]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Placa.Should().Be("ABC1234");
        result.Debitos.Should().HaveCount(2);

        result.Debitos[0].Tipo.Should().Be("IPVA");
        result.Debitos[0].ValorOriginal.Should().Be("1500.00");
        result.Debitos[0].ValorAtualizado.Should().Be("1800.00");
        result.Debitos[0].DiasAtraso.Should().Be(121);

        result.Debitos[1].Tipo.Should().Be("MULTA");
        result.Debitos[1].ValorOriginal.Should().Be("300.50");
        result.Debitos[1].ValorAtualizado.Should().Be("555.93");
        result.Debitos[1].DiasAtraso.Should().Be(85);

        result.Resumo.TotalOriginal.Should().Be("1800.50");
        result.Resumo.TotalAtualizado.Should().Be("2355.93");
    }

    [Fact]
    public async Task Debt_due_after_reference_date_has_zero_overdue()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Ipva(1000m, "2024-06-01")]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Debitos[0].DiasAtraso.Should().Be(0);
        result.Debitos[0].ValorAtualizado.Should().Be("1000.00");
    }

    [Fact]
    public async Task Debt_due_exactly_on_reference_date_has_zero_overdue()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Ipva(1000m, "2024-05-10")]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Debitos[0].DiasAtraso.Should().Be(0);
    }

    // Validação de placa

    [Theory]
    [InlineData("INVALIDA")]
    [InlineData("")]
    public async Task Invalid_plate_throws_before_calling_any_provider(string plate)
    {
        var provider = Substitute.For<IDebtProvider>();

        await FluentActions.Invoking(() => BuildHandler(provider).HandleAsync(new(plate), default))
            .Should().ThrowAsync<InvalidPlateException>();

        await provider.DidNotReceive()
            .GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // Fallback entre provedores

    public static TheoryData<Exception> ProviderExceptions =>
        new() { new HttpRequestException(), new TaskCanceledException() };

    [Theory]
    [MemberData(nameof(ProviderExceptions))]
    public async Task Provider1_failure_falls_back_to_provider2(Exception ex)
    {
        var p1 = Substitute.For<IDebtProvider>();
        var p2 = Substitute.For<IDebtProvider>();
        p1.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(ex);
        p2.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([Ipva()]);

        var result = await BuildHandler(p1, p2).HandleAsync(new("ABC1234"), default);

        result.Debitos.Should().HaveCount(1);
        await p1.Received(1).GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task All_providers_failing_throws_AllProvidersUnavailable()
    {
        var p1 = Substitute.For<IDebtProvider>();
        var p2 = Substitute.For<IDebtProvider>();
        p1.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());
        p2.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException());

        await FluentActions.Invoking(() => BuildHandler(p1, p2).HandleAsync(new("ABC1234"), default))
            .Should().ThrowAsync<AllProvidersUnavailableException>();
    }

    [Fact]
    public async Task Provider1_success_does_not_call_provider2()
    {
        var p1 = Substitute.For<IDebtProvider>();
        var p2 = Substitute.For<IDebtProvider>();
        p1.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([Ipva()]);

        await BuildHandler(p1, p2).HandleAsync(new("ABC1234"), default);

        await p2.DidNotReceive().GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // Tipo desconhecido

    [Theory]
    [InlineData("LICENCIAMENTO")]
    [InlineData("OUTRO")]
    [InlineData("ipva")]  // case-sensitive
    public async Task Unknown_debt_type_throws_with_correct_type_name(string type)
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([new RawDebt(type, 100m, new DateOnly(2024, 1, 1))]);

        var ex = await FluentActions
            .Invoking(() => BuildHandler(provider).HandleAsync(new("ABC1234"), default))
            .Should().ThrowAsync<UnknownDebtTypeException>();

        ex.Which.DebtType.Should().Be(type);
    }

    // Opções de pagamento

    [Fact]
    public async Task Payment_options_order_is_TOTAL_then_per_type_in_debt_order()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Ipva(), Multa()]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Pagamentos.Opcoes.Select(o => o.Tipo)
            .Should().ContainInOrder("TOTAL", "SOMENTE_IPVA", "SOMENTE_MULTA");
    }

    [Fact]
    public async Task Single_debt_type_produces_TOTAL_and_one_SOMENTE()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Ipva()]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Pagamentos.Opcoes.Select(o => o.Tipo)
            .Should().Equal("TOTAL", "SOMENTE_IPVA");
    }

    [Fact]
    public async Task Two_debts_of_same_type_produce_single_SOMENTE_with_summed_value()
    {
        var provider = Substitute.For<IDebtProvider>();
        provider.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([Multa(100m, "2024-01-01"), Multa(200m, "2024-01-01")]);

        var result = await BuildHandler(provider).HandleAsync(new("ABC1234"), default);

        result.Pagamentos.Opcoes.Should().HaveCount(2);
        result.Pagamentos.Opcoes[0].Tipo.Should().Be("TOTAL");
        result.Pagamentos.Opcoes[1].Tipo.Should().Be("SOMENTE_MULTA");

        var someteBase = decimal.Parse(result.Pagamentos.Opcoes[1].ValorBase,
            System.Globalization.CultureInfo.InvariantCulture);
        var totalBase = decimal.Parse(result.Pagamentos.Opcoes[0].ValorBase,
            System.Globalization.CultureInfo.InvariantCulture);
        someteBase.Should().Be(totalBase);
    }
}
