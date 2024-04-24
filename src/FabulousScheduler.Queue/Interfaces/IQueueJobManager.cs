using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Queue.Interfaces;

public interface IQueueJobScheduler : IJobScheduler
{
    /// <summary>
    /// Callback delegate, that will be call every time when any job will completed
    /// </summary>
    /// <param name="sender">Job which sent event</param>
    /// <param name="e">Result</param>
    public delegate void JobResultEventHandler(ref IQueueJob sender, ref JobResult<JobOk, JobFail> e);
}