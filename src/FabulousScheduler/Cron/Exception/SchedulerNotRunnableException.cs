namespace FabulousScheduler.Cron.Exception;

/// <summary>
/// You must register some jobs only after call <see cref="CronJobManager.RunScheduler()"/> />
/// </summary>
public class SchedulerNotRunnableException : System.Exception
{
    public SchedulerNotRunnableException(string message) : base(message) { }
}