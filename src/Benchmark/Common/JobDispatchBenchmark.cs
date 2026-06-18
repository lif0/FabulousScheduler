using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Recurring.Abstraction;
using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Recurring.Result;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// Measures the per-job allocation distribution on the real execution path.
///
/// The question being answered: is the <see cref="Task{T}"/> returned by
/// <c>ExecuteAsync</c> (the candidate for a Task -> ValueTask refactor) a meaningful
/// share of what a single job execution allocates, or is it dwarfed by the
/// <c>Task.Factory.StartNew(async ...)</c> dispatch the scheduler performs per job?
///
/// Compare:
///   ExecuteAsync_*            -> allocation of ExecuteAsync alone
///   Dispatch_StartNew_*       -> allocation of the scheduler's dispatch + ExecuteAsync
/// The delta between them is the dispatch overhead.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[MemoryDiagnoser]
public class JobDispatchBenchmark
{
    private const int Batch = 1000;

    private BenchRecurringJob _asyncJob = null!;
    private BenchRecurringJob _syncJob = null!;
    private readonly ConcurrentQueue<IRecurringJob> _queue = new();
    private SemaphoreSlim _signal = null!;

    [GlobalSetup]
    public void Setup()
    {
        _asyncJob = new BenchRecurringJob(isAsyncAction: true);
        _syncJob = new BenchRecurringJob(isAsyncAction: false);
        _signal = new SemaphoreSlim(0);
    }

    // ---- ExecuteAsync alone ----

    /// <summary> ExecuteAsync on the async path (await of an already-completed action). </summary>
    [Benchmark(Baseline = true)]
    public async Task<JobResult<JobOk, JobFail>> ExecuteAsync_AsyncAction()
    {
        _ = _asyncJob.State; // flip Sleeping -> Ready so the job can run again
        return await _asyncJob.ExecuteAsync();
    }

    /// <summary> ExecuteAsync on the sync path (the ActionJob().Result sync-over-async branch). </summary>
    [Benchmark]
    public async Task<JobResult<JobOk, JobFail>> ExecuteAsync_SyncAction()
    {
        _ = _syncJob.State;
        return await _syncJob.ExecuteAsync();
    }

    // ---- Scheduler-style dispatch (mirrors BaseRecurringScheduler.CreateTask) ----

    /// <summary> Task.Factory.StartNew(async =&gt; await ExecuteAsync()) on the async path. </summary>
    [Benchmark]
    public async Task<JobResult<JobOk, JobFail>> Dispatch_StartNew_AsyncAction()
    {
        _ = _asyncJob.State;
        IRecurringJob job = _asyncJob;
        var outer = Task.Factory.StartNew(static async obj =>
            {
                var j = (IRecurringJob)obj!;
                return await j.ExecuteAsync();
            },
            job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        return await outer.Unwrap();
    }

    /// <summary> Same dispatch, sync path. </summary>
    [Benchmark]
    public async Task<JobResult<JobOk, JobFail>> Dispatch_StartNew_SyncAction()
    {
        _ = _syncJob.State;
        IRecurringJob job = _syncJob;
        var outer = Task.Factory.StartNew(static async obj =>
            {
                var j = (IRecurringJob)obj!;
                return await j.ExecuteAsync();
            },
            job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
        return await outer.Unwrap();
    }

    // ---- Proposed dispatch (Task.Run over a single async wrapper, no Task<Task>/Unwrap) ----

    /// <summary> Task.Run(() =&gt; RunWrap(job)) on the async path. </summary>
    [Benchmark]
    public async Task<JobResult<JobOk, JobFail>> Dispatch_TaskRun_AsyncAction()
    {
        _ = _asyncJob.State;
        IRecurringJob job = _asyncJob;
        return await Task.Run(() => RunWrap(job));
    }

    /// <summary> Same Task.Run dispatch, sync path. </summary>
    [Benchmark]
    public async Task<JobResult<JobOk, JobFail>> Dispatch_TaskRun_SyncAction()
    {
        _ = _syncJob.State;
        IRecurringJob job = _syncJob;
        return await Task.Run(() => RunWrap(job));
    }

    /// <summary>
    /// One async wrapper around ExecuteAsync, mirroring the scheduler's RunJobAsync
    /// (minus the scheduler-state post-processing, which is equal in both dispatch styles).
    /// </summary>
    private static async Task<JobResult<JobOk, JobFail>> RunWrap(IRecurringJob job)
        => await job.ExecuteAsync().ConfigureAwait(false);

    // ---- v5.0.1 dispatch, per-job amortized (directly comparable to WorkerPool_PerJob) ----
    // Same StartNew dispatch as Dispatch_StartNew_*, but OperationsPerInvoke amortizes the
    // benchmark's own wrapper Task away, so the number is the true per-job dispatch allocation.

    /// <summary> Per-job cost of the old StartNew dispatch, async path. </summary>
    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task Dispatch_StartNew_PerJob_AsyncAction()
    {
        for (int i = 0; i < Batch; i++)
        {
            _ = _asyncJob.State;
            IRecurringJob job = _asyncJob;
            await Task.Factory.StartNew(static async obj =>
                {
                    var j = (IRecurringJob)obj!;
                    await j.ExecuteAsync();
                },
                job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .Unwrap().ConfigureAwait(false);
        }
    }

    /// <summary> Per-job cost of the old StartNew dispatch, sync path. </summary>
    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task Dispatch_StartNew_PerJob_SyncAction()
    {
        for (int i = 0; i < Batch; i++)
        {
            _ = _syncJob.State;
            IRecurringJob job = _syncJob;
            await Task.Factory.StartNew(static async obj =>
                {
                    var j = (IRecurringJob)obj!;
                    await j.ExecuteAsync();
                },
                job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default)
                .Unwrap().ConfigureAwait(false);
        }
    }

    // ---- v5.1.0 dispatch: fixed worker pool ----
    // Steady-state per-job cost of a worker draining the queue. The worker Task and its state
    // machine are allocated once per worker, so OperationsPerInvoke amortizes them away and the
    // reported number is what a single job actually costs: queue/signal + ExecuteAsync, with no
    // per-job Task dispatch.

    /// <summary> Per-job cost inside a worker loop, async path. </summary>
    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task WorkerPool_PerJob_AsyncAction()
    {
        for (int i = 0; i < Batch; i++)
        {
            _queue.Enqueue(_asyncJob);
            _signal.Release();
        }
        for (int i = 0; i < Batch; i++)
        {
            await _signal.WaitAsync().ConfigureAwait(false);
            _queue.TryDequeue(out IRecurringJob? job);
            _ = job!.State; // reset Sleeping -> Ready
            _ = await job.ExecuteAsync().ConfigureAwait(false);
        }
    }

    /// <summary> Per-job cost inside a worker loop, sync path. </summary>
    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task WorkerPool_PerJob_SyncAction()
    {
        for (int i = 0; i < Batch; i++)
        {
            _queue.Enqueue(_syncJob);
            _signal.Release();
        }
        for (int i = 0; i < Batch; i++)
        {
            await _signal.WaitAsync().ConfigureAwait(false);
            _queue.TryDequeue(out IRecurringJob? job);
            _ = job!.State; // reset Sleeping -> Ready
            _ = await job.ExecuteAsync().ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Minimal concrete recurring job whose ActionJob returns a pre-built, already-completed
/// task so the benchmark measures the framework path (ExecuteAsync / dispatch), not the
/// payload work or per-call result allocation.
/// </summary>
public sealed class BenchRecurringJob : BaseRecurringJob
{
    private readonly Task<JobResult<JobOk, JobFail>> _completed;

    public BenchRecurringJob(bool isAsyncAction)
        : base(name: "bench", category: "bench", sleepDuration: TimeSpan.Zero, isAsyncAction: isAsyncAction)
    {
        _completed = Task.FromResult<JobResult<JobOk, JobFail>>(new JobOk(ID));
    }

    protected override Task<JobResult<JobOk, JobFail>> ActionJob() => _completed;
}
