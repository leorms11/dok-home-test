using System.Text.Json.Serialization;

namespace VehicleDebts.Application.DTOs;

public record VehicleDebtsResponse(
    [property: JsonPropertyName("placa")]      string Placa,
    [property: JsonPropertyName("debitos")]    IReadOnlyList<DebitDto> Debitos,
    [property: JsonPropertyName("resumo")]     ResumoDto Resumo,
    [property: JsonPropertyName("pagamentos")] PagamentosDto Pagamentos
);

public record DebitDto(
    [property: JsonPropertyName("tipo")]             string Tipo,
    [property: JsonPropertyName("valor_original")]   string ValorOriginal,
    [property: JsonPropertyName("valor_atualizado")] string ValorAtualizado,
    [property: JsonPropertyName("vencimento")]       string Vencimento,
    [property: JsonPropertyName("dias_atraso")]      int DiasAtraso
);

public record ResumoDto(
    [property: JsonPropertyName("total_original")]   string TotalOriginal,
    [property: JsonPropertyName("total_atualizado")] string TotalAtualizado
);

public record PagamentosDto(
    [property: JsonPropertyName("opcoes")] IReadOnlyList<OpcaoPagamentoDto> Opcoes
);

public record OpcaoPagamentoDto(
    [property: JsonPropertyName("tipo")]           string Tipo,
    [property: JsonPropertyName("valor_base")]     string ValorBase,
    [property: JsonPropertyName("pix")]            PixDto Pix,
    [property: JsonPropertyName("cartao_credito")] CartaoCreditoDto CartaoCredito
);

public record PixDto(
    [property: JsonPropertyName("total_com_desconto")] string TotalComDesconto
);

public record CartaoCreditoDto(
    [property: JsonPropertyName("parcelas")] IReadOnlyList<ParcelaDto> Parcelas
);

public record ParcelaDto(
    [property: JsonPropertyName("quantidade")]    int Quantidade,
    [property: JsonPropertyName("valor_parcela")] string ValorParcela
);
