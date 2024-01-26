using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Interfaces;

public interface ICronJobScheduler : IJobScheduler
{
	/// <summary>
	/// Callback delegate, that will be call every time when any job will completed
	/// </summary>
	/// <param name="sender">Job which sent event</param>
	/// <param name="e">Result</param>
	public delegate void JobResultEventHandler(ref ICronJob sender, ref JobResult<JobOk, JobFail> e);
}