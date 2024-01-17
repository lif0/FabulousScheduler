using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Cron.Abstraction;

namespace FabulousScheduler.Cron.Internal;

/// <summary>
/// Cron job manager
/// </summary>
[SuppressMessage("ReSharper", "RedundantBaseQualifier")]
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
internal sealed class CronJobManager : BaseCronJobManager
{
    internal event Cron.CronJobManager.CallbackHandler? CallbackHandler;
    private Task _loop;
    
    internal CronJobManager(Config? config) : base(config)
    {
        _loop = Task.Factory.StartNew(InfLoop,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    /// <summary>
    /// Register a cron job
    /// </summary>
    internal Guid RegisterCron(Func<Task> action, TimeSpan sleepDuration)
    {
        CronJob job = new (action, sleepDuration);
        base.Register(job);
        return job.Id;
    }
    
    
    /// <summary>
    /// Register a cron job
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid RegisterCron(Func<Task> action, string name, TimeSpan sleepDuration)
    {
        CronJob job = new (action, name, sleepDuration);
        base.Register(job);
        return job.Id;
    }

    /// <summary>
    /// Register a cron job
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid RegisterCron(Func<Task> action, string name, string category, TimeSpan sleepDuration)
    {
        CronJob job = new(action, name: name, category, sleepDuration);
        base.Register(job);
        return job.Id;
    }

    /// <summary>
    /// Recheck jobs every <see cref="Config.SleepAfterCheck"/> time
    /// </summary>
    private async void InfLoop()
    {
        while (true)
        {
            try
            {
                var res = await base.ExecuteReadyJob();
                if (res is { Length: > 0 })
                {
                    Parallel.ForEach(res, jres =>
                    {
                        CallbackHandler?.Invoke(jres);
                    });
                }
                await Task.Delay(base.Config.SleepAfterCheck);
            }
            catch (Exception)
            {
                // ignored
            }
        }
        // ReSharper disable once FunctionNeverReturns
    }
    
}