using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Exception;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Internal;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron;

public static class CronJobManager
{
    private static Config? _config;
    private static CronJobScheduler? _scheduler;

    public static event ICronJobScheduler.JobResultEventHandler? JobResultEvent;

    /// <summary>
    /// Set config for CronJobManager
    /// </summary>
    /// <param name="config">Config instance</param>
    /// <exception cref="SetConfigAfterRunSchedulingException"> if you call this method after calling <see cref="RunScheduler"/> method</exception>
    public static void SetConfig(Config config)
    {
        if (_scheduler != null)
        {
            throw new SetConfigAfterRunSchedulingException($"{nameof(_scheduler)} already initialized");
        }
        _config = config;
    }

    /// <summary>
    /// Start a job scheduler
    /// </summary>
    public static void RunScheduler()
    {
        InternalInit();
        _scheduler!.RunScheduler();
    }

    #region RegisterJob

    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A algorithm that should be repeated</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    /// <returns>return JobID</returns>
    public static Guid Register(Func<Task> action, TimeSpan sleepDuration)
    {
        return InternalRegisterJob(action, null, null, sleepDuration);
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
        return InternalRegisterJob(action, name, null, sleepDuration);
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
        return InternalRegisterJob(action, name, category, sleepDuration);
    }

    #endregion

    #region Private

    private static Guid InternalRegisterJob(Func<Task> action, string? name, string? category, TimeSpan sleepDuration)
    {
        InternalInit();
        ArgumentNullException.ThrowIfNull(_scheduler, "You should init CronJobManager");
        switch (name, category)
        {
            case (null, null):
            {
                return _scheduler.RegisterCron(action, sleepDuration);
            } 
            case (not null, null):
            {
                return _scheduler.RegisterCron(action, name, sleepDuration);
            }
            case (not null, not null):
            {
                return _scheduler.RegisterCron(action, name, category, sleepDuration);
            }
            default:
                return _scheduler.RegisterCron(action, sleepDuration);
        }
    }
    
    private static void InternalInit(Config? config = null)
    {
        if (_scheduler != null) return;

        _scheduler = new(config);
        _scheduler.JobResultEvent += 
            (ref ICronJob sender, ref JobResult<JobOk, JobFail> e) =>
                JobResultEvent?.Invoke(ref sender, ref e);
    }

    #endregion
}