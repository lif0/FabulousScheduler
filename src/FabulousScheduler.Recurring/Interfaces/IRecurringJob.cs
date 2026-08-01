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

	/// <summary> Runs the job once and returns its result (<see cref="JobOk"/> or <see cref="JobFail"/>). </summary>
	/// <remarks> Never throws: a failure (including an exception thrown inside the job) is returned as <see cref="JobFail"/>. </remarks>
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();

	/// <summary> Reset state </summary>
	/// <remarks>
	/// Moves the job to <see cref="JobStateEnum.Waiting"/> so the scheduler can pick it up.
	/// </remarks>
	internal void ResetState();
}