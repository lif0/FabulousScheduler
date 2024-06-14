using FabulousScheduler.Exception;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Queue.Internal;
using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue;

/// <summary> Queue job's manager </summary>
public static class QueueJobManager
{
    private static readonly object _syncScheduler = new();
    private static Configuration? _config;
    private static IQueue? _queue;
    private static QueueScheduler? _scheduler;

    /// <summary> Callback job's result </summary>
    public static event IQueueJobScheduler.JobResultEventHandler? JobResultEvent;

    /// <summary> Set config for <see cref="QueueJobManager"/> </summary>
    /// <param name="config"> Configuration instance </param>
    /// <param name="queue"> Queue for job  </param>
    /// <exception cref="SetConfigAfterRunSchedulingException"> If you call this method after calling <see cref="RunScheduler"/> method </exception>
    public static void SetConfig(Configuration config, IQueue queue)
    {
        lock (_syncScheduler)
        {
            if (_scheduler != null)
            {
                throw new SetConfigAfterRunSchedulingException(nameof(QueueScheduler));
            }

            _queue = queue;
            _config = config;
        }
    }

    /// <summary> Start the job's scheduler </summary>
    public static void RunScheduler()
    {
        lock (_syncScheduler)
        {
            if (_scheduler == null)
            {
                InternalInitUnsafe();
                _scheduler!.RunScheduler();
            }
        }
    }

    #region RegisterJob

    /// <summary> Register a job on jobManager </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <returns> return JobID </returns>
    public static Guid Register(Func<Task> action)
    {
        return InternalRegisterJob(
            actionAsync:action
        );
    }

    /// <summary> Register a job </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <param name="name"> The job's name </param>
    /// <returns> return JobID </returns>
    public static Guid Register(Func<Task> action, string name)
    {
        return InternalRegisterJob(
            actionAsync:action,
            name:name
        );
    }
    
    /// <summary> Register a job on jobManager </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <param name="name"> The job's name </param>
    /// <param name="category"> The job's category </param>
    /// <returns> return JobID  </returns>
    public static Guid Register(Func<Task> action, string name, string category)
    {
        return InternalRegisterJob(
            actionAsync:action,
            name:name,
            category:category
        );
    }
    
    /// <summary> Register a job on jobManager </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <returns> return JobID </returns>
    public static Guid Register(Action action)
    {
        return InternalRegisterJob(
            actionSync:action
        );
    }

    /// <summary> Register a job on jobManager </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <param name="name">The job's name</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Action action, string name)
    {
        return InternalRegisterJob(
            actionSync:action,
            name:name
        );
    }
    
    /// <summary> Register a job on jobManager </summary>
    /// <param name="action"> A action that should be repeated </param>
    /// <param name="name"> The job's name </param>
    /// <param name="category"> The job's category </param>
    /// <returns> return JobID </returns>
    public static Guid Register(Action action, string name, string category)
    {
        return InternalRegisterJob(
            actionSync:action,
            name:name,
            category:category
        );
    }

    #endregion

    #region Private

    private static Guid InternalRegisterJob(Action? actionSync = null, Func<Task>? actionAsync = null, string? name = null, string? category = null, byte? attempts = null)
    {
        lock (_syncScheduler)
        {
            if (_scheduler == null)
            {
                throw new SchedulerNotRunnableException(nameof(QueueJobManager));
            }
        }

        if (actionSync == null && actionAsync == null)
        {
            throw new ArgumentNullException(nameof(actionSync)+" and "+nameof(actionAsync), "one of action must be not null");
        }

        if (actionSync != null)
        {
            return _scheduler.Register(actionSync, name, category, attempts);
        }

        return _scheduler.Register(actionAsync!, name, category, attempts); 
    }
    
    private static void InternalInitUnsafe()
    {
        _scheduler = new(_config, _queue!);
        _scheduler.JobResultEvent += 
            (ref IQueueJob sender, ref JobResult<JobOk, JobFail> e) =>
                JobResultEvent?.Invoke(ref sender, ref e);
    }

    #endregion
}