namespace FabulousScheduler.Core.Interfaces.Result;

/// <summary>
/// The result of successful completion of a job
/// </summary>
public interface IJobOk
{
    /// <summary>
    /// <inheritdoc cref="IJob.ID"/>
    /// </summary>
    public Guid ID  { get; }
}