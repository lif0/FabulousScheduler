using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Cron.Result;

/// <summary>
///  <inheritdoc cref="IJobOk"/>
/// </summary>
public class JobOk : IJobOk
{
    public JobOk(Guid jobID)
    {
        ID = jobID;
    }
    
    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public Guid ID { get; }
}