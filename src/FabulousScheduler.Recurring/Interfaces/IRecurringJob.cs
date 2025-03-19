using FabulousScheduler.Recurring.Result;
using FabulousScheduler.Recurring.Enums;
using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Recurring.Interfaces;

/// <summary> The recurring job </summary>
public interface IRecurringJob: IJob
{
	/// <summary> Job's category </summary>
	public string Category { get; }
	
	/// <summary> Job's <see cref="JobStateEnum">State</see> </summary>
	public State State { get; }
	
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
	/// Set <see cref="JobStateEnumJobStateEnuming"/> for <see cref="State"/> 
	/// </remarks>
	internal void ResetState();
}