using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Cron.Abstraction;

namespace FabulousScheduler.Cron.Internal;

/// <summary>
/// Cron job manager
/// </summary>
[SuppressMessage("ReSharper", "RedundantBaseQualifier")]
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
internal sealed class CronJobScheduler : BaseCronJobScheduler
{
    internal CronJobScheduler(Config? config) : base(config)
    {
    }

    /// <summary>
    /// Register a cron job
    /// </summary>
    internal Guid RegisterCron(Func<Task> action, TimeSpan sleepDuration)
    {
        CronJob job = new (action, sleepDuration);
        base.Register(job);
        return job.ID;
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
        return job.ID;
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
        return job.ID;
    }
}