using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;
using FabulousScheduler.Queue.Result;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// End-to-end throughput of the queue scheduler: enqueue N jobs that each run once, start the
/// scheduler and wait until the last result arrives. Exercises the worker pool and the Channel work
/// queue together.
///
/// Each job does no real work, so the number is the scheduler's overhead per job.
/// <c>OperationsPerInvoke = N</c> makes it per-job: Mean is time/job (throughput = 1e9 / Mean ns),
/// Allocated is bytes/job.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 5, iterationCount: 15, invocationCount: 1)]
[MemoryDiagnoser]
public class QueueThroughputBenchmark
{
    private const int N = 100_000;

    private BenchQueueScheduler _scheduler = null!;
    private TaskCompletionSource _done = null!;
    private int _count;

    [IterationSetup]
    public void Setup()
    {
        _count = 0;
        _done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var queue = new InMemoryQueue();
        for (int i = 0; i < N; i++)
        {
            queue.Enqueue(new ThroughputQueueJob());
        }

        _scheduler = new BenchQueueScheduler(new Configuration(maxParallelJobExecute: Environment.ProcessorCount), queue);
        _scheduler.JobResultEvent += OnResult;
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

    private void OnResult(ref IQueueJob sender, ref JobResult<JobOk, JobFail> e)
    {
        if (Interlocked.Increment(ref _count) == N)
        {
            _done.TrySetResult();
        }
    }
}

/// <summary>Queue scheduler with a public ctor for benchmarking.</summary>
public sealed class BenchQueueScheduler : BaseQueueScheduler
{
    public BenchQueueScheduler(Configuration config, IQueue queue) : base(config, queue) { }
}

/// <summary>Runs once and returns a fresh result.</summary>
public sealed class ThroughputQueueJob : BaseQueueJob
{
    public ThroughputQueueJob() : base("t", "t", isAsyncAction: true, attempts: null) { }

    protected override Task<JobResult<JobOk, JobFail>> ActionJob()
        => Task.FromResult<JobResult<JobOk, JobFail>>(new JobOk(ID, Name));
}
