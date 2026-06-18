namespace FabulousScheduler.Core.Interfaces;

/// <summary> Job scheduler </summary>
public interface IJobScheduler : IDisposable
{
    /// <summary> Starts the job scheduler </summary>
    public void RunScheduler();
}