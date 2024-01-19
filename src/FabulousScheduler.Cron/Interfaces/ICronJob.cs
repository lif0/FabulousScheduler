using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Interfaces;

public interface ICronJob: IJob
{
	/// <summary>
	/// Job's category
	/// </summary>
	public string Category { get; }
	
	/// <summary>
	/// Job's <see cref="CronJobStateEnum">State</see> 
	/// </summary>
	public CronJobStateEnum State { get; }
	
	/// <summary>
	/// Sleep duration after success finish job
	/// </summary>
	/// <remarks>
	/// How much time will the job be asleep after success execute before that get into the task pool again
	/// </remarks>
	public TimeSpan SleepDuration { get; }
	public long TotalRun { get; }
	public long TotalFail { get; }
	
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();

	public void SetStateWaiting();
}