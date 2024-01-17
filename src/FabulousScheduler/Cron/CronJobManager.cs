using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron;

public static class CronJobManager
{
#pragma warning disable CS8618
    private static Internal.CronJobManager _manager;
#pragma warning restore CS8618
    
    public delegate void CallbackHandler(JobResult<JobOk, JobFail> e);
    public static event CallbackHandler? CallbackEvent;
    

    /// <summary>
    /// Initialize CronJobManager 
    /// </summary>
    /// <remarks>With <see cref="Config.Default"/> configs</remarks>
    public static void Init()
    {
        InternalInit();
    }
    
    /// <summary>
    /// Initialize CronJobManager with configs
    /// </summary>
    /// <param name="config"></param>
    public static void Init(Config config)
    {
        InternalInit(config);
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
        switch (name, category)
        {
            case (null, null):
            {
                return _manager.RegisterCron(action, sleepDuration);
            } 
            case (not null, null):
            {
                return _manager.RegisterCron(action, name, sleepDuration);
            }
            case (not null, not null):
            {
                return _manager.RegisterCron(action, name, category, sleepDuration);
            }
            default:
                return _manager.RegisterCron(action, sleepDuration);
        }
    }
    
    private static void InternalInit(Config? config = null)
    {
        _manager = new(config ?? Config.Default);
        _manager.CallbackHandler += result => CallbackEvent?.Invoke(result);
    }

    #endregion
}
