namespace FabulousScheduler.Core.Interfaces.Result;

/// <summary> The result of a successfully completed  job </summary>
public interface IJobOk
{
    /// <summary> <inheritdoc cref="IJob.ID"/> </summary>
    public Guid ID  { get; }
}