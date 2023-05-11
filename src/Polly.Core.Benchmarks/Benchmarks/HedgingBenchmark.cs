using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Polly.Core.Benchmarks;

namespace Polly.Benchmarks;

public class HedgingBenchmark
{
    private ResilienceStrategy? _strategy;

    [GlobalSetup]
    public void Setup()
    {
        _strategy = Helper.CreateHedging();
    }

    [Benchmark(Baseline = true)]
    public async ValueTask Hedging_Primary()
        => await _strategy!.ExecuteValueTaskAsync(static _ => new ValueTask<string>("primary")).ConfigureAwait(false);

    [Benchmark]
    public async ValueTask Hedging_Secondary()
        => await _strategy!.ExecuteValueTaskAsync(static _ => new ValueTask<string>(Helper.Failure)).ConfigureAwait(false);

    [Benchmark]
    public async ValueTask Hedging_Primary_AsyncWork()
        => await _strategy!.ExecuteValueTaskAsync(
            static async _ =>
            {
                await Task.Yield();
                return "primary";
            }).ConfigureAwait(false);

    [Benchmark]
    public async ValueTask Hedging_Secondary_AsyncWork()
        => await _strategy!.ExecuteValueTaskAsync(
            static async _ =>
            {
                await Task.Yield();
                return Helper.Failure;
            }).ConfigureAwait(false);
}