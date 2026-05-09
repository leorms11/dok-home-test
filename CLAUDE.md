# CLAUDE.md

This file provides guidance to Claude Code when working with this repository.

## Overview

.NET 10 solution with three services: a main API (`VehicleDebts.*`) that aggregates vehicle debt data from two simulated external providers (`YourDebits` and `DebtsOnline`), calculates overdue interest and simulates payment options.

## Solution Structure

```
HomeTest.sln
├── src/
│   ├── YourDebits/               — Provider A (JSON, port 5002) — Minimal API
│   ├── DebtsOnline/              — Provider B (XML, port 5127)  — MVC Controller
│   └── Dok/
│       ├── VehicleDebts.Api/         — ASP.NET Core API (port 5014)
│       ├── VehicleDebts.Application/ — Use cases, interest calculators, payment simulator, logging
│       ├── VehicleDebts.Domain/      — Interfaces, value objects, exceptions, enums
│       ├── VehicleDebts.Infrastructure/ — HTTP providers, circuit breaker, DI
│       └── VehicleDebts.Tests/       — xUnit tests (unit + integration)
```

## Commands

```bash
# Build entire solution
dotnet build HomeTest.sln

# Run all tests
dotnet test HomeTest.sln

# Run a single test by name
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Start providers
dotnet run --project src/YourDebits/YourDebits.csproj --launch-profile http
dotnet run --project src/DebtsOnline/DebtsOnline.csproj --launch-profile http

# Start main API
dotnet run --project src/Dok/VehicleDebts.Api/VehicleDebts.Api.csproj --launch-profile http
```

## Architecture

Follows **Clean Architecture** — dependencies flow inward:
`Api → Application → Domain ← Infrastructure`

Key patterns: CQRS (`IQueryHandler`), Strategy (`IInterestCalculator`), Decorator (`CircuitBreakerDebtProvider`), Adapter (JSON/XML providers → `RawDebt`).

## Key Domain Rules

- **Reference date:** `2024-05-10` (fixed for tests)
- **IPVA interest:** 0.33%/day, capped at 20% of original amount
- **MULTA interest:** 1.00%/day, no cap
- **Rounding:** HALF_UP, 2 decimal places — applied on the final sum (original + interest)
- **Payment options:** PIX (5% discount), credit card 1x/6x/12x (PMT formula at 2.5%/month)
- **Unknown debt type:** throws `UnknownDebtTypeException` → HTTP 422 (fail-fast, do not silently ignore)

## Logging

Each request emits a **single structured JSON log entry** at the end of the pipeline via `RequestLoggingMiddleware`. The `RequestLogContext` (scoped) accumulates data across all layers.

- Plate is masked for LGPD compliance: `ABC1234` → `ABC****` (applied in path and log fields)
- `RequestPath` property is removed from Serilog output via `RemovePropertiesEnricher`
- Provider attempts include name, success, duration, circuit breaker state, and error if any

## Resilience

- **Circuit Breaker** per provider: opens after 3 failures (`FailureThreshold`), stays open for 30 s (`OpenDurationSeconds`) — configured in `appsettings.json`
- **Fallback:** YourDebits → DebtsOnline (order defined in `Infrastructure/DependencyInjection.cs`)

## Feature Flags (YourDebits and DebtsOnline)

Both providers expose runtime toggles for testing resilience scenarios:

```
POST /api/feature-flag/toggle/api      — enable/disable the provider entirely
POST /api/feature-flag/toggle/delay    — enable/disable artificial delay
PATCH /api/feature-flag/delay?delayMs= — set delay duration in ms
```

## Seed Note

`YourDebits` seed intentionally contains a `LICENCIAMENTO` debt (unknown type) to trigger a 422 and force fallback to `DebtsOnline`. Remove/comment the entry in `src/YourDebits/Repositories/DebtRepository.cs` to disable this behaviour.

## Test Suite

76 tests — 75 passing, 1 skipped (Kestrel body-size limit not testable via TestServer).

| File | Layer | Focus |
|---|---|---|
| `Domain/PlateTests.cs` | Domain | Plate validation and normalization |
| `Domain/IpvaInterestCalculatorTests.cs` | Domain | IPVA interest with cap boundary |
| `Domain/MultaInterestCalculatorTests.cs` | Domain | MULTA interest, no cap |
| `Application/GetVehicleDebtsHandlerTests.cs` | Application | Full use case, fallback, edge cases |
| `Application/PaymentSimulatorServiceTests.cs` | Application | PIX discount, PMT formula |
| `Infrastructure/CircuitBreakerTests.cs` | Infrastructure | State machine + thread-safety |
| `Integration/VehicleDebtsApiIntegrationTests.cs` | Integration | HTTP contract, status codes, middleware |