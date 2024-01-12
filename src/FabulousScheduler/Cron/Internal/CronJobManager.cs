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
    internal void RegisterCron(Func<Task> action, TimeSpan sleepDuration)
    {
        CronJob job = new (action, sleepDuration);
        base.Register(job);
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