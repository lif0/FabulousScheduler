using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Cron.Abstraction;

namespace FabulousScheduler.Cron.Internal;

/// <summary> Cron job manager </summary>
[SuppressMessage("ReSharper", "RedundantBaseQualifier")]
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
internal sealed class CronScheduler : BaseCronScheduler
{
    internal CronScheduler(Configuration? config) : base(config)
    {
    }

    #region RegisterJob

    /// <summary>
    /// Register a cron job
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid RegisterCron(Action action, string? name, string? category, TimeSpan sleepDuration)
    {
        CronJob job = new(action, name, category, sleepDuration);
        base.Register(job);
        return job.ID;
    }
    
    /// <summary>
    /// Register a cron job
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name(optional)</param>
    /// <param name="category">The job's category(optional)</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid RegisterCron(Func<Task> action, string? name, string? category, TimeSpan sleepDuration)
    {
        CronJob job = new(action, name, category, sleepDuration);
        base.Register(job);
        return job.ID;
    }

    #endregion
}