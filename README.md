# VehicleDebts — Consulta e Simulação de Débitos Veiculares

Serviço que consulta débitos veiculares em múltiplos provedores externos, normaliza os dados, calcula juros por atraso e simula formas de pagamento.

---

## Como rodar

### Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### 1. Iniciar os provedores simulados

```bash
# Provedor A — JSON (porta 5002)
dotnet run --project src/YourDebits/YourDebits.csproj --launch-profile http

# Provedor B — XML (porta 5127)
dotnet run --project src/DebtsOnline/DebtsOnline.csproj --launch-profile http
```

### 2. Iniciar a API principal

```bash
dotnet run --project src/Dok/VehicleDebts.Api/VehicleDebts.Api.csproj --launch-profile http
```

A API ficará disponível em `http://localhost:5014`.

### 3. Consultar débitos

```bash
curl http://localhost:5014/api/vehicle/ABC1234/debts
```

### Rodar testes

```bash
dotnet test HomeTest.sln
```

---

## Portas

| Serviço         | HTTP            |
|-----------------|-----------------|
| VehicleDebts.Api | `localhost:5014` |
| YourDebits       | `localhost:5002` |
| DebtsOnline      | `localhost:5127` |

---

## Feature Flags — YourDebits e DebtsOnline

Ambos os provedores expõem endpoints de controle em runtime para facilitar testes de resiliência.

### Habilitar / Desabilitar a API

Alterna o provider inteiro. Quando desabilitado, todas as requisições retornam `503`.

```bash
# Desabilitar
POST http://localhost:5002/api/feature-flag/toggle/api   # YourDebits
POST http://localhost:5127/api/feature-flag/toggle/api   # DebtsOnline

# Habilitar novamente (mesmo endpoint — é um toggle)
POST http://localhost:5002/api/feature-flag/toggle/api
```

### Habilitar / Desabilitar delay artificial

Útil para simular timeout e acionar o circuit breaker.

```bash
# Ativar delay
POST http://localhost:5002/api/feature-flag/toggle/delay   # YourDebits
POST http://localhost:5127/api/feature-flag/toggle/delay   # DebtsOnline
```

### Configurar o tempo de delay (ms)

```bash
# Definir delay de 6000 ms (aciona timeout de 5 s configurado no VehicleDebts.Api)
PATCH http://localhost:5002/api/feature-flag/delay?delayMs=6000   # YourDebits
PATCH http://localhost:5127/api/feature-flag/delay?delayMs=6000   # DebtsOnline
```

> **Nota:** O timeout de cada provider no `VehicleDebts.Api` é de **5 segundos** (configurável em `appsettings.json` → `Providers.*.TimeoutSeconds`). Um delay ≥ 5000 ms forçará o fallback para o próximo provider e incrementará o contador do circuit breaker.

---

## Seed de débitos e o tipo LICENCIAMENTO

O seed do **YourDebits** contém intencionalmente um débito do tipo `LICENCIAMENTO`:

```csharp
// src/YourDebits/Repositories/DebtRepository.cs
new() { ..., Type = DebtType.LICENCIAMENTO, Amount = 1000.00m, ... }
```

`LICENCIAMENTO` não é reconhecido pelo `VehicleDebts.Api` (apenas `IPVA` e `MULTA` são suportados), portanto a resposta do YourDebits causará um `UnknownDebtTypeException` → HTTP 422. Isso força o motor de fallback a tentar o **DebtsOnline** como segundo provider, simulando o cenário de degradação real.

Para desativar esse comportamento e receber uma resposta normal do YourDebits, basta remover ou comentar a linha do seed no arquivo acima.

---

## Decisões Técnicas

### Arquitetura — Clean Architecture em camadas

```
VehicleDebts.Domain          → entidades, interfaces, exceções, value objects
VehicleDebts.Application     → use cases, calculadoras de juros, simulador de pagamento, logging
VehicleDebts.Infrastructure  → provedores HTTP, circuit breaker, DI de infra
VehicleDebts.Api             → controller, middlewares, Serilog, Program.cs
VehicleDebts.Tests           → testes unitários e de integração
```

A dependência flui para dentro: `Api → Application → Domain ← Infrastructure`.

### Padrões utilizados

| Padrão | Onde |
|---|---|
| **CQRS (Query)** | `IQueryHandler<TQuery, TResult>` — separa leitura de escrita desde a interface |
| **Strategy** | `IInterestCalculator` — cada tipo de débito tem sua calculadora isolada |
| **Decorator** | `CircuitBreakerDebtProvider` — envolve qualquer `IDebtProvider` com resiliência |
| **Adapter** | `YourDebitsProvider` / `DebtsOnlineProvider` — normalizam JSON e XML para `RawDebt` |
| **Ports & Adapters** | `IDebtProvider` é o port; os providers são os adapters |
| **Value Object** | `Plate` — encapsula e valida o formato de placa (antigo e Mercosul) |
| **Circuit Breaker** | `CircuitBreaker` — estados Closed → Open → HalfOpen por provider |

### Log estruturado e consolidado

Cada request emite **uma única entrada JSON** ao final do ciclo de vida, contendo:

```json
{
  "Method": "GET",
  "Path": "/api/vehicle/ABC****/debts",
  "StatusCode": 200,
  "DurationMs": 245,
  "Plate": "ABC****",
  "Providers": [
    { "Name": "YourDebits", "Success": false, "DurationMs": 12, "HealthState": "Closed",
      "Error": { "Type": "UnknownDebtTypeException", "Message": "..." } },
    { "Name": "DebtsOnline", "Success": true,  "DurationMs": 98, "HealthState": "Closed", "Error": null }
  ],
  "Debts": { "Count": 2, "Types": ["IPVA","MULTA"], "TotalOriginal": 1800.50, "TotalUpdated": 2355.93 },
  "Error": null
}
```

A placa é mascarada em todos os campos — inclusive no path — para conformidade com a **LGPD** (`ABC1234` → `ABC****`).

### Resiliência

- **Circuit Breaker** por provider: após 3 falhas consecutivas (`FailureThreshold`), o circuito abre por 30 s (`OpenDurationSeconds`). Configurável em `appsettings.json`.
- **Fallback**: o handler tenta os providers na ordem de registro; o primeiro a responder com sucesso é utilizado.
- **Timeout** por provider: configurável em `appsettings.json` → `Providers.*.TimeoutSeconds`.

---

## Trade-offs

| Decisão | Alternativa descartada | Motivo |
|---|---|---|
| Path parameter `GET /api/vehicle/{plate}/debts` | POST com JSON body `{"placa":"ABC1234"}` | Operação de leitura idempotente; GET permite cache HTTP e é mais RESTful |
| Circuit breaker próprio (manual) | Polly | Sem dependência extra; suficiente para o escopo; demonstra o padrão diretamente |
| `IRequestLogContext` como scoped service | Passar contexto por parâmetro | Evita poluir as assinaturas de todos os métodos; acumulação transparente nas camadas |
| `Math.Pow` em `double` para cálculo PMT | `Pow` iterativo em `decimal` | Precisão resultante é `~10⁻¹⁵`, irrelevante após arredondamento HALF_UP a 2 casas |
| Fail-fast em tipo de débito desconhecido | Ignorar e processar apenas os conhecidos | Silenciar um tipo desconhecido mascararia inconsistências de dados do provider |
| `IProviderHealthInfo` no Domain para expor estado do CB | Expor via infra ou injetar `IHttpContextAccessor` no singleton | Mantém Infrastructure fora do Application sem criar referência circular; Domain define o contrato |

---

## Melhorias Futuras

- **Teste de zero débitos** — cenário documentado no enunciado mas sem cobertura automatizada.
- **Retry com backoff exponencial** — hoje o fallback é direto; adicionar retry (ex.: via Polly) antes de abrir o circuit breaker aumentaria a resiliência.
- **Observabilidade** — exportar métricas (taxa de falha por provider, latência p95) via OpenTelemetry.
- **Cache de resposta** — resultados por placa poderiam ser cacheados por TTL curto (`IMemoryCache` ou Redis) para reduzir carga nos provedores.
- **Configuração de provedores em runtime** — hoje a ordem e os endpoints dos provedores são fixos em `appsettings.json`; tornar isso dinâmico permitiria adicionar/remover provedores sem redeploy.
- **Suporte a novos tipos de débito** — o padrão Strategy (`IInterestCalculator`) já suporta extensão sem modificação; bastaria registrar uma nova calculadora no DI.
- **Estratégia para dados divergentes entre provedores** — atualmente o primeiro provider que responde com sucesso "vence". Uma estratégia mais robusta poderia comparar os resultados de múltiplos provedores e usar votação por maioria ou preferência configurável.
- **Decimal Pow nativo** — substituir `Math.Pow` (double) por multiplicação iterativa em `decimal` eliminaria qualquer drift de precisão no cálculo PMT.

---

## Divergências do Enunciado e Justificativas

### 1. Formato de entrada — Path parameter em vez de JSON body

**Enunciado:** `{ "placa": "ABC1234" }` como entrada.  
**Implementação:** `GET /api/vehicle/{plate}/debts`

Operação de leitura idempotente — GET com path parameter é mais alinhado com REST semântico, permite cache HTTP e evita overhead de parsing de body para um único campo escalar. O enunciado apresenta apenas o dado de entrada, sem especificar método HTTP nem content-type.

---

### 2. Gatilho do HTTP 422 — Primeiro tipo desconhecido vs. "todos desconhecidos"

**Enunciado (Requisitos):** "HTTP 422 quando **todos** os débitos são de tipo desconhecido."  
**Enunciado (Regras de Negócio):** "Tipos não previstos devem **causar erro** HTTP 422. **Não silenciar**."

As duas seções são contraditórias. A implementação segue a regra mais restritiva (fail-fast no primeiro tipo desconhecido), evitando silenciar inconsistências e alinhando-se com o princípio de não converter para "OUTROS".

---

### 3. Sequência de arredondamento dos juros

**Enunciado:** arredonda o juro intermediário e depois soma ao original.  
**Implementação:** soma primeiro, arredonda o total.

Ambas as estratégias produzem o mesmo resultado para os exemplos do enunciado. O próprio enunciado declara tolerância de ±R$ 0,02 para variações de arredondamento intermediário. A abordagem atual reduz propagação de erro de arredondamento.

---

### 4. Precisão do fator PMT — `Math.Pow` em `double`

**Enunciado:** fórmula PMT com `(1+i)^n`.  
**Implementação:** `(decimal)Math.Pow((double)(1 + i), n)`

`decimal` nativo do .NET não possui operação de potência. A conversão via `double` introduz imprecisão da ordem de `10⁻¹⁵`, irrelevante após arredondamento HALF_UP a 2 casas. Todos os valores esperados são produzidos corretamente (validados pelos testes).

---

### 5. Caso de borda sem cobertura de teste — Zero débitos

O cenário "provedor responde com lista vazia" é suportado em runtime mas não possui teste automatizado. A implementação retorna corretamente uma resposta com listas vazias e totais zerados.
