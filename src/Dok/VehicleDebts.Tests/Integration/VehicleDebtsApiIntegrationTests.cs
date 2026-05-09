using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Tests.Integration;

public class VehicleDebtsApiIntegrationTests
{
    private static HttpClient CreateClient(Action<IServiceCollection> configure)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureServices(services =>
            {
                services.RemoveAll<IDebtProvider>();
                configure(services);
            }));
        return factory.CreateClient();
    }

    private static IDebtProvider ProviderWith(IReadOnlyList<RawDebt> debts)
    {
        var p = Substitute.For<IDebtProvider>();
        p.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(debts);
        return p;
    }

    private static IDebtProvider FailingProvider()
    {
        var p = Substitute.For<IDebtProvider>();
        p.GetDebtsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("provider down"));
        return p;
    }

    private static RawDebt Ipva()  => new("IPVA",  1500.00m, new DateOnly(2024, 1, 10));
    private static RawDebt Multa() => new("MULTA",  300.50m, new DateOnly(2024, 2, 15));

    // 1 — Contrato completo do spec
    [Fact]
    public async Task Full_spec_contract_matches_exactly()
    {
        var client = CreateClient(s => s.AddSingleton(ProviderWith([Ipva(), Multa()])));

        var response = await client.GetAsync("/api/vehicle/ABC1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        json.GetProperty("placa").GetString().Should().Be("ABC1234");

        var debitos = json.GetProperty("debitos");
        debitos[0].GetProperty("tipo").GetString().Should().Be("IPVA");
        debitos[0].GetProperty("valor_original").GetString().Should().Be("1500.00");
        debitos[0].GetProperty("valor_atualizado").GetString().Should().Be("1800.00");
        debitos[0].GetProperty("dias_atraso").GetInt32().Should().Be(121);

        debitos[1].GetProperty("tipo").GetString().Should().Be("MULTA");
        debitos[1].GetProperty("valor_original").GetString().Should().Be("300.50");
        debitos[1].GetProperty("valor_atualizado").GetString().Should().Be("555.93");
        debitos[1].GetProperty("dias_atraso").GetInt32().Should().Be(85);

        var resumo = json.GetProperty("resumo");
        resumo.GetProperty("total_original").GetString().Should().Be("1800.50");
        resumo.GetProperty("total_atualizado").GetString().Should().Be("2355.93");

        var opcoes = json.GetProperty("pagamentos").GetProperty("opcoes");

        opcoes[0].GetProperty("tipo").GetString().Should().Be("TOTAL");
        opcoes[0].GetProperty("valor_base").GetString().Should().Be("2355.93");
        opcoes[0].GetProperty("pix").GetProperty("total_com_desconto").GetString().Should().Be("2238.13");
        var totalParcelas = opcoes[0].GetProperty("cartao_credito").GetProperty("parcelas");
        totalParcelas[0].GetProperty("valor_parcela").GetString().Should().Be("2355.93");
        totalParcelas[1].GetProperty("valor_parcela").GetString().Should().Be("427.72");
        totalParcelas[2].GetProperty("valor_parcela").GetString().Should().Be("229.67");

        opcoes[1].GetProperty("tipo").GetString().Should().Be("SOMENTE_IPVA");
        var ipvaParcelas = opcoes[1].GetProperty("cartao_credito").GetProperty("parcelas");
        ipvaParcelas[1].GetProperty("valor_parcela").GetString().Should().Be("326.79");
        ipvaParcelas[2].GetProperty("valor_parcela").GetString().Should().Be("175.48");

        opcoes[2].GetProperty("tipo").GetString().Should().Be("SOMENTE_MULTA");
        var multaParcelas = opcoes[2].GetProperty("cartao_credito").GetProperty("parcelas");
        multaParcelas[1].GetProperty("valor_parcela").GetString().Should().Be("100.93");
        multaParcelas[2].GetProperty("valor_parcela").GetString().Should().Be("54.20");
    }

    // 2 — Placa inválida → 400
    [Fact]
    public async Task Invalid_plate_returns_400()
    {
        var client = CreateClient(s => s.AddSingleton(ProviderWith([])));

        var response = await client.GetAsync("/api/vehicle/INVALIDA/debts");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("invalid_plate");
    }

    // 3 — Tipo desconhecido → 422
    [Fact]
    public async Task Unknown_debt_type_returns_422()
    {
        var provider = ProviderWith([new RawDebt("LICENCIAMENTO", 500m, new DateOnly(2024, 1, 1))]);
        var client = CreateClient(s => s.AddSingleton(provider));

        var response = await client.GetAsync("/api/vehicle/ABC1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("unknown_debt_type");
        json.GetProperty("type").GetString().Should().Be("LICENCIAMENTO");
    }

    // 4 — Todos os providers falham → 503
    [Fact]
    public async Task All_providers_down_returns_503()
    {
        var client = CreateClient(s =>
        {
            s.AddSingleton(FailingProvider());
            s.AddSingleton(FailingProvider());
        });

        var response = await client.GetAsync("/api/vehicle/ABC1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("error").GetString().Should().Be("all_providers_unavailable");
    }

    // 5 — Placa Mercosul é aceita
    [Fact]
    public async Task Mercosul_plate_is_accepted()
    {
        var client = CreateClient(s => s.AddSingleton(ProviderWith([Ipva()])));

        var response = await client.GetAsync("/api/vehicle/ABC1D23/debts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("placa").GetString().Should().Be("ABC1D23");
    }

    // 6 — Kestrel body size limit (não verificável via TestServer)
    [Fact(Skip = "TestServer bypasses Kestrel — MaxRequestBodySize não é aplicado in-process")]
    public Task Request_body_over_1MiB_returns_413() => Task.CompletedTask;

    // 7 — UnmappedMemberHandling não quebra resposta GET
    [Fact]
    public async Task Response_is_normal_when_no_unknown_fields_in_output()
    {
        var client = CreateClient(s => s.AddSingleton(ProviderWith([Ipva()])));

        var response = await client.GetAsync("/api/vehicle/ABC1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 8 — Provider 1 falha, provider 2 responde → 200
    [Fact]
    public async Task Provider1_failure_falls_back_to_provider2()
    {
        var client = CreateClient(s =>
        {
            s.AddSingleton(FailingProvider());
            s.AddSingleton(ProviderWith([Ipva(), Multa()]));
        });

        var response = await client.GetAsync("/api/vehicle/ABC1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("debitos").GetArrayLength().Should().Be(2);
    }

    // 9 — Placa em minúsculo é normalizada
    [Fact]
    public async Task Lowercase_plate_is_normalized_in_response()
    {
        var client = CreateClient(s => s.AddSingleton(ProviderWith([Ipva()])));

        var response = await client.GetAsync("/api/vehicle/abc1234/debts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("placa").GetString().Should().Be("ABC1234");
    }
}
