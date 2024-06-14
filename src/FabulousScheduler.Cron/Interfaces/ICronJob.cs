using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;
using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Cron.Interfaces;

/// <summary> Cron job </summary>
public interface ICronJob: IJob
{
	/// <summary> Job's category </summary>
	public string Category { get; }
	
	/// <summary> Job's <see cref="CronJobStateEnum">State</see> </summary>
	public CronJobStateEnum State { get; }
	
	/// <summary>
	/// Sleep duration after success finish job
	/// </summary>
	/// <remarks>
	/// How much time will the job be asleep after success execute before that get into the task pool again
	/// </remarks>
	public TimeSpan SleepDuration { get; }

	/// <summary> Total run </summary>
	public ulong TotalRun { get; }

	/// <summary> Total fail </summary>
	public ulong TotalFail { get; }

	/// <summary>
	/// TODO KGG :> add description
	/// </summary>
	/// <returns></returns>
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();

	/// <summary> Reset state </summary>
	/// <remarks>
	/// Set <see cref="CronJobStateEnum.Waiting"/> for <see cref="State"/> 
	/// </remarks>
	internal void ResetState();
}