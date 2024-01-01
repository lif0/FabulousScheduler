namespace JobScheduler.Cron.Enums;

[Flags]
public enum CronJobFailEnum
{
	/// <summary>
	/// Job have incorrect start.
	/// <example>
	///		When a job begin execute with state not equal <see cref="CronJobStateEnum.Ready"/>
	/// </example>
	/// </summary>
	IncorrectState,
	
	/// <summary>
	/// When in a job throw any exception
	/// </summary>
	InternalException,
	
	/// <summary>
	///	When a job is failed
	/// </summary>
	FailedExecute,
	
	/// <summary>
	/// Job is disposed
	/// </summary>
	Disposed,
}