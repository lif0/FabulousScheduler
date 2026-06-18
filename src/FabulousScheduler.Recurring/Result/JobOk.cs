using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Recurring.Result;

/// <summary> <inheritdoc cref="IJobOk"/> </summary>
public sealed class JobOk : IJobOk
{
    public JobOk(Guid jobID)
    {
        ID = jobID;
    }
    
    /// <summary> <inheritdoc cref="IJobOk.ID"/> </summary>
    public Guid ID { get; }
}