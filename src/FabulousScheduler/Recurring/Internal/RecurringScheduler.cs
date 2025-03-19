using FabulousScheduler.Recurring.Abstraction;
using System.Diagnostics.CodeAnalysis;

namespace FabulousScheduler.Recurring.Internal;

/// <summary> Recurring job manager </summary>
[SuppressMessage("ReSharper", "RedundantBaseQualifier")]
[SuppressMessage("ReSharper", "NotAccessedField.Local")]
internal sealed class RecurringScheduler : BaseRecurringScheduler
{
    internal RecurringScheduler(Configuration? config) : base(config)
    {
    }

    #region RegisterJob

    /// <summary>
    /// Register the recurring job
    /// </summary>
    /// <param name="action">An action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid Register(Action action, string? name, string? category, TimeSpan sleepDuration)
    {
        RecurringJob job = new(action, name, category, sleepDuration);
        base.Register(job);
        return job.ID;
    }
    
    /// <summary>
    /// Register the recurring job
    /// </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name(optional)</param>
    /// <param name="category">The job's category(optional)</param>
    /// <param name="sleepDuration">How long time a job will be sleep after success execute</param>
    internal Guid Register(Func<Task> action, string? name, string? category, TimeSpan sleepDuration)
    {
        RecurringJob job = new(action, name, category, sleepDuration);
        base.Register(job);
        return job.ID;
    }

    #endregion
}