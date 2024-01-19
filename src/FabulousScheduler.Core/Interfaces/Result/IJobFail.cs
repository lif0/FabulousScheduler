namespace FabulousScheduler.Core.Interfaces.Result;

/// <summary>
/// The result of fail completion of a job
/// </summary>
public interface IJobFail
{
    /// <summary>
    /// <inheritdoc cref="IJob.ID"/>
    /// </summary>
    public Guid ID  { get; }
}