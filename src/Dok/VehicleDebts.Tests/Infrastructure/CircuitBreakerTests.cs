using FluentAssertions;
using VehicleDebts.Infrastructure.Resilience;

namespace VehicleDebts.Tests.Infrastructure;

public class CircuitBreakerTests
{
    private const int Threshold = 3;

    private static CircuitBreaker Create(TimeSpan? openDuration = null) =>
        new("test", Threshold, openDuration ?? TimeSpan.FromSeconds(30));

    private static Task FailAsync(CircuitBreaker cb) =>
        cb.ExecuteAsync<bool>(_ => throw new InvalidOperationException("forced"), default)
          .ContinueWith(_ => { }, TaskContinuationOptions.OnlyOnFaulted);

    [Fact]
    public async Task Successful_call_executes_action_and_returns_result()
    {
        var cb = Create();
        var result = await cb.ExecuteAsync(_ => Task.FromResult(42), default);
        result.Should().Be(42);
    }

    [Fact]
    public async Task Below_threshold_failures_keep_circuit_closed()
    {
        var cb = Create();
        for (var i = 0; i < Threshold - 1; i++)
            await FailAsync(cb);

        var executed = false;
        await cb.ExecuteAsync(async _ => { executed = true; return true; }, default);
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Reaching_threshold_opens_circuit()
    {
        var cb = Create();
        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        await FluentActions.Invoking(() => cb.ExecuteAsync(_ => Task.FromResult(true), default))
            .Should().ThrowAsync<CircuitBreakerOpenException>();
    }

    [Fact]
    public async Task Open_circuit_does_not_call_action()
    {
        var cb = Create();
        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        var called = false;
        try { await cb.ExecuteAsync(async _ => { called = true; return true; }, default); }
        catch (CircuitBreakerOpenException) { }

        called.Should().BeFalse();
    }

    [Fact]
    public async Task Expired_open_duration_allows_probe_call()
    {
        var cb = Create(TimeSpan.FromMilliseconds(50));
        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        await Task.Delay(120);

        var executed = false;
        await cb.ExecuteAsync(async _ => { executed = true; return true; }, default);
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task HalfOpen_success_returns_circuit_to_closed()
    {
        var cb = Create(TimeSpan.FromMilliseconds(50));
        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        await Task.Delay(120);
        await cb.ExecuteAsync(_ => Task.FromResult(true), default); // probe — succeeds

        // Circuit must be Closed: accepts further calls normally
        var executed = false;
        await cb.ExecuteAsync(async _ => { executed = true; return true; }, default);
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task HalfOpen_failure_returns_circuit_to_open()
    {
        var cb = Create(TimeSpan.FromMilliseconds(50));
        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        await Task.Delay(120);
        await FailAsync(cb); // probe — fails

        await FluentActions.Invoking(() => cb.ExecuteAsync(_ => Task.FromResult(true), default))
            .Should().ThrowAsync<CircuitBreakerOpenException>();
    }

    [Fact]
    public async Task Success_resets_failure_counter_so_threshold_applies_again()
    {
        var cb = Create();
        for (var i = 0; i < Threshold - 1; i++)
            await FailAsync(cb);

        await cb.ExecuteAsync(_ => Task.FromResult(true), default); // resets counter

        for (var i = 0; i < Threshold; i++)
            await FailAsync(cb);

        await FluentActions.Invoking(() => cb.ExecuteAsync(_ => Task.FromResult(true), default))
            .Should().ThrowAsync<CircuitBreakerOpenException>();
    }

    [Fact]
    public async Task Concurrent_failures_result_in_consistent_open_state()
    {
        var cb = Create();
        var tasks = Enumerable.Range(0, 30)
            .Select(_ => Task.Run(() => FailAsync(cb)))
            .ToArray();
        await Task.WhenAll(tasks);

        await FluentActions.Invoking(() => cb.ExecuteAsync(_ => Task.FromResult(true), default))
            .Should().ThrowAsync<CircuitBreakerOpenException>();
    }
}
