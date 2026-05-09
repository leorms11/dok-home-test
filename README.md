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

## Testes

**76 testes no total — 75 passando, 1 ignorado** (limitação de infra documentada abaixo).

Os testes estão organizados em cinco camadas, cada uma cobrindo uma responsabilidade distinta.

---

### Domain — `PlateTests` (4 testes)

Valida o value object `Plate`, que é a primeira barreira de entrada da aplicação.

| Teste | Por quê |
|---|---|
| Placa no formato antigo (`ABC1234`) e Mercosul (`ABC1D23`) são aceitas | Garante que os dois padrões legais do Brasil passam pela validação |
| Placa em minúsculo e com espaços é normalizada para maiúsculo sem espaços | O sistema deve aceitar entrada "suja" do cliente sem rejeitar indevidamente |
| Formatos inválidos lançam `InvalidPlateException` | Impede que placas malformadas cheguem aos providers |
| String vazia ou só espaços lança `InvalidPlateException` | Protege contra input em branco que passaria validações superficiais |

---

### Domain — `IpvaInterestCalculatorTests` e `MultaInterestCalculatorTests` (13 testes)

Cobrem as duas estratégias de cálculo de juros isoladamente, sem dependência de outros componentes.

| Teste | Por quê |
|---|---|
| `CanHandle` retorna `true` apenas para o tipo correto | Garante que o Strategy pattern não roteia débitos para a calculadora errada |
| IPVA com 121 dias ativa o teto de 20% (min(598,95, 300,00) = 300,00) | Valida o caso do enunciado onde o teto é determinante |
| IPVA com 60 dias fica abaixo do teto, com 61 dias o teto é ativado exatamente | Testa a fronteira precisa do teto |
| Dias ≤ 0 retornam juro zero (não vencido / dias negativos) | Cobre o caso de borda de débito com vencimento futuro |
| MULTA com 85 dias → 255,425 (sem arredondamento no calculator) | Confirma que o arredondamento é responsabilidade do handler, não da calculadora |
| MULTA sem teto cresce linearmente (200 dias → 200% do valor) | Confirma ausência de cap na MULTA |

---

### Application — `GetVehicleDebtsHandlerTests` (11 testes)

Testa o caso de uso principal com providers mockados (NSubstitute), isolando a lógica de negócio da infraestrutura.

| Teste | Por quê |
|---|---|
| Exemplo completo do enunciado produz valores exatos (IPVA 1800,00 / MULTA 555,93) | Teste de regressão direto contra o contrato do enunciado |
| Débito com vencimento após a data de referência tem 0 dias de atraso e sem juros | Cobre o caso de borda de débito não vencido |
| Débito vencendo exatamente na data de referência tem 0 dias de atraso | Testa a fronteira inclusive da comparação de datas |
| Placa inválida lança exceção antes de chamar qualquer provider | Garante que a validação ocorre antes de qualquer I/O |
| Falha do provider 1 (HttpRequestException e TaskCanceledException) faz fallback para o provider 2 | Valida o mecanismo de fallback para os dois tipos de erro mais comuns em HTTP |
| Todos os providers falhando lança `AllProvidersUnavailableException` | Garante que o handler não engole a falha silenciosamente |
| Sucesso no provider 1 não chama o provider 2 | Confirma que o fallback é lazy — evita chamadas desnecessárias |
| Tipo de débito desconhecido lança `UnknownDebtTypeException` com o tipo correto no payload | Valida o fail-fast e o preenchimento correto da propriedade do erro |
| Ordem das opções de pagamento é TOTAL → SOMENTE_IPVA → SOMENTE_MULTA | O enunciado especifica a ordem; quebrá-la é um bug de contrato |
| Dois débitos do mesmo tipo geram apenas um `SOMENTE_<TIPO>` com o valor somado | Agrupa corretamente em vez de criar entradas duplicadas |

---

### Application — `PaymentSimulatorServiceTests` (7 testes)

Testa o simulador de pagamento de forma isolada, usando os valores esperados do enunciado como referência.

| Teste | Por quê |
|---|---|
| PIX aplica 5% de desconto sobre `valor_base` de cada opção | O desconto deve ser individual por opção, não apenas no TOTAL |
| Cartão 1x = `valor_base` (sem juros) | O enunciado é explícito: 1x à vista não tem acréscimo |
| PMT 6x e 12x para todos os valores do enunciado (tolerância ±0,02) | Valida a fórmula Price contra os exemplos oficiais |
| Cada opção tem exatamente as parcelas 1, 6 e 12 | Impede que parcelas adicionais apareçam na resposta |

---

### Infrastructure — `CircuitBreakerTests` (9 testes)

Testa a máquina de estados do circuit breaker em isolamento, incluindo thread-safety.

| Teste | Por quê |
|---|---|
| Chamada bem-sucedida executa a ação e retorna o resultado | Smoke test do caminho feliz |
| N-1 falhas mantêm o circuito fechado | O threshold deve ser a fronteira exata |
| N falhas abrem o circuito | Garante que o mecanismo de proteção é ativado corretamente |
| Circuito aberto não chama a ação | Proteger o downstream é o propósito central do padrão |
| Duração de abertura expirada permite chamada de sonda (HalfOpen) | Valida a transição para recuperação |
| Sonda bem-sucedida fecha o circuito | Garante que o circuito se recupera após falha temporária |
| Sonda falhando reabre o circuito | Garante que uma recuperação prematura não passa o circuito para Closed |
| Sucesso reseta o contador de falhas | Sem reset, o threshold acumularia falhas de sessões distintas |
| 30 falhas concorrentes resultam em estado Open consistente | Verifica que o `lock` garante thread-safety sem race condition |

---

### Integration — `VehicleDebtsApiIntegrationTests` (9 testes, 1 ignorado)

Sobem a aplicação em memória via `WebApplicationFactory`, substituindo apenas os providers por mocks. Testam o pipeline HTTP completo: roteamento, middlewares, serialização JSON e códigos de status.

| Teste | Por quê |
|---|---|
| Contrato completo do enunciado (todos os campos, valores e nomes) | Única camada que valida a serialização JSON final — nomes de campos, tipos e estrutura |
| Placa inválida → HTTP 400 com `{"error":"invalid_plate"}` | Valida que o `ExceptionHandlingMiddleware` mapeia corretamente a exceção |
| Tipo desconhecido → HTTP 422 com `{"error":"unknown_debt_type","type":"LICENCIAMENTO"}` | Valida payload completo do erro 422 incluindo o campo `type` |
| Todos os providers falham → HTTP 503 com `{"error":"all_providers_unavailable"}` | Valida o contrato de resposta de indisponibilidade total |
| Placa Mercosul (`ABC1D23`) é aceita e retornada corretamente | Garante que o novo padrão de placa funciona ponta a ponta |
| Fallback de provider em nível HTTP → 200 com dados do provider 2 | Testa o fallback no pipeline real, não apenas na lógica do handler |
| Placa em minúsculo na URL é normalizada na resposta | Garante que a normalização do value object reflete na resposta HTTP |
| Limite de 1 MiB do Kestrel → *ignorado* | O `TestServer` não passa por Kestrel; o limite é configurado mas não testável in-process |

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
