using System.Diagnostics;
using System.Globalization;
using VehicleDebts.Application.Abstractions;
using VehicleDebts.Application.DTOs;
using VehicleDebts.Application.Services;
using VehicleDebts.Domain.Enums;
using VehicleDebts.Domain.Exceptions;
using VehicleDebts.Domain.Interfaces;
using VehicleDebts.Domain.ValueObjects;

namespace VehicleDebts.Application.UseCases.GetVehicleDebts;

public sealed class GetVehicleDebtsHandler(
    IEnumerable<IDebtProvider> providers,
    IEnumerable<IInterestCalculator> calculators,
    PaymentSimulatorService paymentSimulator,
    IRequestLogContext logCtx)
    : IQueryHandler<GetVehicleDebtsQuery, VehicleDebtsResponse>
{
    private static readonly DateOnly ReferenceDate = new(2024, 5, 10);

    public async Task<VehicleDebtsResponse> HandleAsync(GetVehicleDebtsQuery query, CancellationToken ct)
    {
        var plate    = new Plate(query.Plate);
        logCtx.SetPlate(plate.Value);
        var rawDebts  = await FetchDebtsAsync(plate.Value, ct);
        var processed = ProcessDebts(rawDebts);
        logCtx.SetDebtsResult(
            processed.Count,
            processed.Select(d => d.Type.ToString()),
            processed.Sum(d => d.OriginalAmount),
            processed.Sum(d => d.UpdatedAmount));
        return BuildResponse(plate.Value, processed);
    }

    private async Task<IReadOnlyList<RawDebt>> FetchDebtsAsync(string plate, CancellationToken ct)
    {
        foreach (var provider in providers)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var result = await provider.GetDebtsAsync(plate, ct);
                sw.Stop();
                logCtx.AddProviderAttempt(
                    provider.Name, success: true, sw.ElapsedMilliseconds,
                    (provider as IProviderHealthInfo)?.HealthState ?? "N/A");
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                logCtx.AddProviderAttempt(
                    provider.Name, success: false, sw.ElapsedMilliseconds,
                    (provider as IProviderHealthInfo)?.HealthState ?? "N/A",
                    ex);
            }
        }

        throw new AllProvidersUnavailableException();
    }

    private List<ProcessedDebt> ProcessDebts(IReadOnlyList<RawDebt> rawDebts)
    {
        var processed = new List<ProcessedDebt>(rawDebts.Count);

        foreach (var raw in rawDebts)
        {
            if (!Enum.TryParse<DebtType>(raw.Type, ignoreCase: false, out var debtType))
                throw new UnknownDebtTypeException(raw.Type);

            var calculator = calculators.FirstOrDefault(c => c.CanHandle(debtType))
                ?? throw new UnknownDebtTypeException(raw.Type);

            var daysOverdue   = Math.Max(0, ReferenceDate.DayNumber - raw.DueDate.DayNumber);
            var interest      = calculator.Calculate(raw.Amount, daysOverdue);
            var updatedAmount = Math.Round(raw.Amount + interest, 2, MidpointRounding.AwayFromZero);

            processed.Add(new ProcessedDebt(debtType, raw.Amount, updatedAmount, raw.DueDate, daysOverdue));
        }

        return processed;
    }

    private VehicleDebtsResponse BuildResponse(string plate, List<ProcessedDebt> debts)
    {
        var totalOriginal = debts.Sum(d => d.OriginalAmount);
        var totalUpdated  = debts.Sum(d => d.UpdatedAmount);

        var debitDtos = debts.Select(d => new DebitDto(
            d.Type.ToString(),
            Money(d.OriginalAmount),
            Money(d.UpdatedAmount),
            d.DueDate.ToString("yyyy-MM-dd"),
            d.DaysOverdue
        )).ToList();

        var paymentOptions = new List<(string Tipo, decimal ValorBase)> { ("TOTAL", totalUpdated) };

        foreach (var group in debts.GroupBy(d => d.Type))
            paymentOptions.Add(($"SOMENTE_{group.Key}", group.Sum(d => d.UpdatedAmount)));

        return new VehicleDebtsResponse(
            plate,
            debitDtos,
            new ResumoDto(Money(totalOriginal), Money(totalUpdated)),
            new PagamentosDto(paymentSimulator.Simulate(paymentOptions))
        );
    }

    private static string Money(decimal value) =>
        value.ToString("F2", CultureInfo.InvariantCulture);
}

internal sealed record ProcessedDebt(
    DebtType Type,
    decimal OriginalAmount,
    decimal UpdatedAmount,
    DateOnly DueDate,
    int DaysOverdue
);