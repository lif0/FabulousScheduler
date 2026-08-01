using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Recurring;
using FabulousScheduler.Recurring.Abstraction;
using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Recurring.Result;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// End-to-end throughput of the recurring scheduler: register N jobs that each run once, start the
/// scheduler and wait until the last result arrives. This exercises everything together — the heap
/// producer, the worker pool and the Channel work queue.
///
/// Each job does no real work (it just returns a result), so the number is the scheduler's own
/// overhead per job. <c>OperationsPerInvoke = N</c> makes the result per-job:
/// Mean is time/job (throughput = 1e9 / Mean ns), Allocated is bytes/job (result + payload + tasks
/// + scheduling).
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 5, iterationCount: 15, invocationCount: 1)]
[MemoryDiagnoser]
public class RecurringThroughputBenchmark
{
    private const int N = 100_000;

    private BenchRecurringScheduler _scheduler = null!;
    private TaskCompletionSource _done = null!;
    private int _count;

    [IterationSetup]
    public void Setup()
    {
        _count = 0;
        _done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _scheduler = new BenchRecurringScheduler(new Configuration(maxParallelJobExecute: Environment.ProcessorCount));
        _scheduler.JobResultEvent += OnResult;

        var jobs = new List<IRecurringJob>(N);
        for (int i = 0; i < N; i++)
        {
            jobs.Add(new ThroughputRecurringJob());
        }
        _scheduler.Register(jobs);
    }

    [Benchmark(OperationsPerInvoke = N)]
    public void RunAll()
    {
        _scheduler.RunScheduler();
        _done.Task.GetAwaiter().GetResult();
    }

    [IterationCleanup]
    public void Cleanup()
    {
        _scheduler.JobResultEvent -= OnResult;
        _scheduler.Dispose();
    }

    private void OnResult(ref IRecurringJob sender, ref JobResult<JobOk, JobFail> e)
    {
        if (Interlocked.Increment(ref _count) == N)
        {
            _done.TrySetResult();
        }
    }
}

/// <summary>Recurring scheduler with a public ctor/Register for benchmarking.</summary>
public sealed class BenchRecurringScheduler : BaseRecurringScheduler
{
    public BenchRecurringScheduler(Configuration config) : base(config) { }
    public new int Register(IEnumerable<IRecurringJob> jobs) => base.Register(jobs);
}

/// <summary>Runs once (MaxValue sleep = never re-scheduled) and returns a fresh result.</summary>
public sealed class ThroughputRecurringJob : BaseRecurringJob
{
    public ThroughputRecurringJob() : base("t", "t", TimeSpan.MaxValue, isAsyncAction: true) { }

    protected override Task<JobResult<JobOk, JobFail>> ActionJob()
        => Task.FromResult<JobResult<JobOk, JobFail>>(new JobOk(ID));
}
