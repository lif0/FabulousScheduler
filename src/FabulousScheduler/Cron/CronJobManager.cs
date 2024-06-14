using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Internal;
using FabulousScheduler.Cron.Result;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Exception;

namespace FabulousScheduler.Cron;

/// <summary> Cron job's manager </summary>
public static class CronJobManager
{
    private static readonly object _syncScheduler = new();
    private static Configuration? _config;
    private static CronScheduler? _scheduler;

    /// <summary>
    /// Callback job's result
    /// </summary>
    public static event ICronJobScheduler.JobResultEventHandler? JobResultEvent;

    /// <summary>
    /// Set config for CronJobManager
    /// </summary>
    /// <param name="config">Config instance</param>
    /// <exception cref="SetConfigAfterRunSchedulingException"> if you call this method after calling <see cref="RunScheduler"/> method</exception>
    public static void SetConfig(Configuration config)
    {
        lock (_syncScheduler)
        {
            if (_scheduler != null)
            {
                throw new SetConfigAfterRunSchedulingException(nameof(CronScheduler));
            }
            _config = config;
        }
    }

    /// <summary>
    /// Start the job's scheduler
    /// </summary>
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

    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Func<Task> action, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionAsync:action
        );
    }

    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Func<Task> action, string name, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionAsync:action,
            name:name
        );
    }
    
    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Func<Task> action, string name, string category, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionAsync:action,
            name:name,
            category:category
        );
    }
    
    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Action action, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionSync:action
        );
    }

    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Action action, string name, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionSync:action,
            name:name
        );
    }
    
    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Action action, string name, string category, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(
            sleepDuration, 
            actionSync:action,
            name:name,
            category:category
        );
    }

    #endregion

    #region Private

    private static Guid InternalRegisterJob(TimeSpan sleepDuration, Action? actionSync = null, Func<Task>? actionAsync = null, string? name = null, string? category = null)
    {
        lock (_syncScheduler)
        {
            if (_scheduler == null)
            {
                throw new SchedulerNotRunnableException(nameof(CronJobManager));
            }
        }

        if (actionSync == null && actionAsync == null)
        {
            throw new ArgumentNullException(nameof(actionSync)+" and "+nameof(actionAsync), "one of action must be not null");
        }

        if (actionSync != null)
        {
            return _scheduler.RegisterCron(actionSync, name, category, sleepDuration);
        }

        return _scheduler.RegisterCron(actionAsync!, name, category, sleepDuration); 
    }
    
    private static void InternalInitUnsafe()
    {
        _scheduler = new(_config);
        _scheduler.JobResultEvent += 
            (ref ICronJob sender, ref JobResult<JobOk, JobFail> e) =>
                JobResultEvent?.Invoke(ref sender, ref e);
    }

    #endregion
}