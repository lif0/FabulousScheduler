using FabulousScheduler.Recurring.Result;
using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Recurring.Interfaces;

public interface IRecurringJobScheduler : IJobScheduler
{
	/// <summary>
	/// Callback delegate, that will be call every time when any job will complete
	/// </summary>
	/// <param name="sender">Job which sent event</param>
	/// <param name="e">Result</param>
	public delegate void JobResultEventHandler(ref IRecurringJob sender, ref JobResult<JobOk, JobFail> e);
}