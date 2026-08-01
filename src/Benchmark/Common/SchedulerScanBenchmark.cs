using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FabulousScheduler.Recurring.Interfaces;
using State = FabulousScheduler.Recurring.Enums.JobStateEnum;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// The recurring producer has to answer "which job runs next?" among N registered jobs.
///
/// v5.0.1/v5.1.0 does it by scanning ALL registered jobs every poll
/// (`Where(State == Ready).OrderBy(LastExecute)`) — O(n), independent of how many are ready.
/// The proposed producer keeps a min-heap keyed by next-run time, so the next job is the heap
/// root — O(log n) to pop and re-insert.
///
/// Both run over N jobs that are currently sleeping (the common idle case), so `Scan` finds
/// nothing yet still pays O(n), while `Heap_Next` pays O(log n).
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[MemoryDiagnoser]
public class SchedulerScanBenchmark
{
    [Params(1_000, 5_000, 50_000)]
    public int N;

    private Dictionary<Guid, IRecurringJob> _registered = null!;
    private PriorityQueue<IRecurringJob, DateTime> _heap = null!;

    [GlobalSetup]
    public void Setup()
    {
        _registered = new Dictionary<Guid, IRecurringJob>(N);
        _heap = new PriorityQueue<IRecurringJob, DateTime>(N);

        for (int i = 0; i < N; i++)
        {
            var job = new BenchRecurringJob(isAsyncAction: true, sleepDuration: TimeSpan.FromHours(1));
            // run once so State == Sleeping (not ready) for the next hour
            job.ExecuteAsync().GetAwaiter().GetResult();

            _registered[job.ID] = job;
            _heap.Enqueue(job, (job.LastExecute ?? DateTime.Now) + TimeSpan.FromHours(1));
        }
    }

    /// <summary> Current producer: O(n) scan + sort of every registered job. </summary>
    [Benchmark(Baseline = true)]
    public int Scan()
    {
        return _registered
            .Where(x => x.Value.State == State.Ready)
            .Select(x => x.Value)
            .OrderBy(x => x.LastExecute)
            .ToArray()
            .Length;
    }

    /// <summary> Proposed producer: O(log n) to pop the next-due job and re-insert it. </summary>
    [Benchmark]
    public DateTime Heap_Next()
    {
        _heap.TryPeek(out _, out DateTime when);
        IRecurringJob job = _heap.Dequeue();     // O(log n)
        _heap.Enqueue(job, when);                // O(log n), keeps heap size stable
        return when;
    }
}
