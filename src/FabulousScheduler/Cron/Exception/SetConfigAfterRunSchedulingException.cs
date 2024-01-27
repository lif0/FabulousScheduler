using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Cron.Exception;

/// <summary>
/// You must call <see cref="CronJobManager.SetConfig"/> before <see cref="IJobScheduler.RunScheduler"/>
/// </summary>
public class SetConfigAfterRunSchedulingException : System.Exception
{
    public SetConfigAfterRunSchedulingException(string message) : base(message)
    {
        
    }
}