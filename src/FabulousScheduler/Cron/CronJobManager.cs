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

    /// <summary>
    /// Register a job on jobManager
    /// </summary>
    /// <param name="action">A algorithm that should be repeated</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    public static void Register(Func<Task> action, TimeSpan sleepDuration)
    {
        _manager.RegisterCron(action, sleepDuration);
    }

    #region Private

    private static void InternalInit(Config? config = null)
    {
        _manager = new(config ?? Config.Default);
        _manager.CallbackHandler += result => CallbackEvent?.Invoke(result);
    }

    #endregion
}
