using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Core.Types;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// Measures the overhead of <see cref="JobResult{TOk,TFail}"/>: how much it allocates
/// (it is a reference type, so every result is a heap allocation) and how costly the
/// success/fail dispatch via <c>Do</c>/<c>Match</c> is.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
public class JobResultBenchmark
{
    // The success/fail payloads are created once so the benchmarks only measure JobResult itself.
    private readonly BenchOk _ok = new(Guid.NewGuid());
    private readonly BenchFail _fail = new(Guid.NewGuid());

    private JobResult<BenchOk, BenchFail> _successResult = null!;
    private JobResult<BenchOk, BenchFail> _failResult = null!;

    // Pre-allocated, non-capturing delegates so we measure dispatch, not delegate allocation.
    private static readonly Action<BenchOk> OnOk = static _ => { };
    private static readonly Action<BenchFail> OnFail = static _ => { };
    private static readonly Func<BenchOk, bool> MatchOk = static _ => true;
    private static readonly Func<BenchFail, bool> MatchFail = static _ => false;

    [GlobalSetup]
    public void Setup()
    {
        _successResult = _ok;   // implicit operator (TOk -> JobResult)
        _failResult = _fail;    // implicit operator (TFail -> JobResult)
    }

    // ---- creation / allocation ----

    [Benchmark(Baseline = true)]
    public JobResult<BenchOk, BenchFail> Create_Success() => _ok;

    [Benchmark]
    public JobResult<BenchOk, BenchFail> Create_Fail() => _fail;

    // ---- Do dispatch ----

    [Benchmark]
    public void Do_Success() => _successResult.Do(OnOk, OnFail);

    [Benchmark]
    public void Do_Fail() => _failResult.Do(OnOk, OnFail);

    // ---- Match<TResult> dispatch ----

    [Benchmark]
    public bool Match_Success() => _successResult.Match(MatchOk, MatchFail);

    [Benchmark]
    public bool Match_Fail() => _failResult.Match(MatchOk, MatchFail);

    // ---- accessors ----

    [Benchmark]
    public Guid JobID_Success() => _successResult.JobID;

    [Benchmark]
    public Guid JobID_Fail() => _failResult.JobID;
}

/// <summary> Minimal <see cref="IJobOk"/> payload used to isolate JobResult overhead. </summary>
public sealed class BenchOk : IJobOk
{
    public BenchOk(Guid id) => ID = id;
    public Guid ID { get; }
}

/// <summary> Minimal <see cref="IJobFail"/> payload used to isolate JobResult overhead. </summary>
public sealed class BenchFail : IJobFail
{
    public BenchFail(Guid id) => ID = id;
    public Guid ID { get; }
}
