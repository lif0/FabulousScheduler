namespace FabulousScheduler.Core.Interfaces;

/// <summary> Job scheduler </summary>
public interface IJobScheduler : IDisposable
{
    /// <summary> Start the job scheduler </summary>
    public void RunScheduler();
}