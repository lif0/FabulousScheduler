using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

// ReSharper disable ClassNeverInstantiated.Global

/// <summary>
/// One producer/consumer handoff through the work queue: enqueue a job, then take it.
///
/// Up to v5.0.1 <c>InMemoryQueue</c> used a lock + a Queue + a TaskCompletionSource, and a take
/// returned a <see cref="Task{T}"/> (a heap object every time). v5.1.0 uses a <see cref="Channel{T}"/>,
/// and a take returns a <see cref="ValueTask{T}"/> that is already completed when a job is waiting,
/// so the common (non-empty) path allocates nothing.
///
/// Both rows measure the non-empty path (a job is always available), which is the steady state under
/// load. <see cref="object"/> stands in for the job so we measure the queue, not the job.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, launchCount: 1, warmupCount: 3, iterationCount: 5)]
[MemoryDiagnoser]
public class QueueHandoffBenchmark
{
    private const int Batch = 1000;
    private static readonly object Item = new();

    private readonly object _lock = new();
    private readonly Queue<object> _oldQueue = new();
    private Channel<object> _channel = null!;

    [GlobalSetup]
    public void Setup()
    {
        _channel = Channel.CreateUnbounded<object>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = false
        });
    }

    /// <summary> v5.0.1 style: lock + Queue + Task.FromResult on take (one Task per take). </summary>
    [Benchmark(Baseline = true, OperationsPerInvoke = Batch)]
    public async Task OldStyle_Lock_Task()
    {
        for (int i = 0; i < Batch; i++)
        {
            lock (_lock) { _oldQueue.Enqueue(Item); }
            Task<object> take;
            lock (_lock) { take = Task.FromResult(_oldQueue.Dequeue()); }
            _ = await take.ConfigureAwait(false);
        }
    }

    /// <summary> v5.1.0 style: Channel + ValueTask on take (no allocation when a job is ready). </summary>
    [Benchmark(OperationsPerInvoke = Batch)]
    public async Task Channel_ValueTask()
    {
        for (int i = 0; i < Batch; i++)
        {
            _channel.Writer.TryWrite(Item);
            _ = await _channel.Reader.ReadAsync().ConfigureAwait(false);
        }
    }
}
