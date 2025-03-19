using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Internal;

/// <summary> Queue-based job manager </summary>
public class QueueScheduler : BaseQueueScheduler
{
    internal QueueScheduler(Configuration? config, IQueue queue) : base(config, queue)
    {
    }
    
    #region RegisterJob

    /// <summary> Register a queue job </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="attempts"> How many time try do that job </param>
    internal Guid Register(Action action, string? name, string? category, byte? attempts)
    {
        QueueJob job = new(action, name, category, attempts);
        base.Queue.Enqueue(job);
        return job.ID;
    }
    
    /// <summary> Register a queue job </summary>
    /// <param name="action">A action that should be repeated</param>
    /// <param name="name">The job's name</param>
    /// <param name="category">The job's category</param>
    /// <param name="attempts"> How many time try do that job </param>
    internal Guid Register(Func<Task> action, string? name, string? category, byte? attempts)
    {
        QueueJob job = new(action, name, category, attempts);
        base.Queue.Enqueue(job);
        return job.ID;
    }

    #endregion
    
}