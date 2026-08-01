using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Abstraction;

public class BaseQueueScheduler : IQueueJobScheduler
{
    // sync
    private readonly object _mainLoopLocker = new();

    // private
    private Task[]? _workers;
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

        _cancellationTokenSource = new CancellationTokenSource();
    }

    #region public

    public void RunScheduler()
    {
        lock (_mainLoopLocker)
        {
            if (_workers != null) return;

            int workerCount = Configuration.MaxParallelJobExecute;
            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                workers[i] = Task.Run(WorkerLoop);
            }

            _workers = workers;
        }
    }

    #endregion

    #region Private

    /// <summary>
    /// A fixed pool of <see cref="Configuration.MaxParallelJobExecute"/> workers consumes the
    /// queue. Each worker pulls one job at a time and awaits it, so the parallelism is bounded
    /// by the worker count instead of a semaphore, and there is no per-job Task dispatch.
    /// </summary>
    private async Task WorkerLoop()
    {
        var token = _cancellationTokenSource.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                IQueueJob job = await Queue.NextAsync(token).ConfigureAwait(false);
                var res = await job.ExecuteAsync().ConfigureAwait(false);
                try
                {
                    JobResultEvent?.Invoke(ref job, ref res);
                }
                catch
                {
                    // a user result handler threw — swallow it so it can't kill the worker
                }
            }
        }
        catch (OperationCanceledException)
        {
            // scheduler is shutting down
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        // let the workers observe the cancellation and finish before freeing anything
        try { if (_workers is { } workers) Task.WaitAll(workers); } catch { /* ignore shutdown errors */ }

        _cancellationTokenSource.Dispose();
        _workers = null;
    }

    #endregion
}
