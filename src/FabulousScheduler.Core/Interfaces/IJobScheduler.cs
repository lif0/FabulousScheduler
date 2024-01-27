using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Core.Interfaces;

/// <summary>
/// Job scheduler
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Start the job scheduler
    /// </summary>
    public void RunScheduler();
}