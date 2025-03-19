using FabulousScheduler.Queue.Interfaces;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace FabulousScheduler.Queue.Abstraction;

public class BaseQueueScheduler : IQueueJobScheduler
{
    // sync
    private readonly object _mainLoopLocker = new();

    // private
    private Task? _mainLoop;
    private readonly SemaphoreSlim _jobExecutorLimiter;
    private readonly ConcurrentDictionary<Guid, (IQueueJob, Task)> _inProgress;
    private readonly CancellationTokenSource _cancellationTokenSource;

    // protected
    protected readonly IQueue Queue;
    protected readonly Configuration Configuration;

    // public
    public event IQueueJobScheduler.JobResultEventHandler? JobResultEvent;

    protected BaseQueueScheduler(Configuration? config, IQueue queue)
    {
        Configuration = config ?? Configuration.Default;
        Queue = queue;

        _jobExecutorLimiter = new SemaphoreSlim(Configuration.MaxParallelJobExecute, Configuration.MaxParallelJobExecute);
        _inProgress = new ConcurrentDictionary<Guid, (IQueueJob, Task)>(Environment.ProcessorCount, this.Configuration.MaxParallelJobExecute);
        _cancellationTokenSource = new CancellationTokenSource();
    }

    #region public

    public void RunScheduler()
    {
        lock (_mainLoopLocker)
        {
            if(_mainLoop != null) return;

            _mainLoop = Task.Factory.StartNew(ExecutableLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }

    #endregion

    #region Private

    private async void ExecutableLoop()
    {
        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            await _jobExecutorLimiter.WaitAsync(CancellationToken.None);
            var newJob = await Queue.NextAsync();
            CreateTask(ref newJob);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CreateTask(ref IQueueJob job)
    {
        var task = Task.Factory.StartNew(async obj =>
        {
            var @job = obj as IQueueJob;
            if (@job is null)
            {
                ArgumentNullException.ThrowIfNull(@job, nameof(@job));
            }

            var res = await @job.ExecuteAsync();
            if (_inProgress.TryRemove(@job.ID, out var tup))
            {
                JobResultEvent?.Invoke(ref tup.Item1, ref res);
            }
            _jobExecutorLimiter.Release(1);
        }, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

        _inProgress.TryAdd(job.ID, (job, task));
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
        _mainLoop?.Dispose();
        _jobExecutorLimiter.Dispose();
        _inProgress.Clear();
    }

    #endregion
}