namespace FabulousScheduler.Recurring.Enums;

[Flags]
public enum JobFailEnum
{
	/// <summary>
	/// The job has an incorrect start
	/// <example>
	///		When a job begins execution with a state not equal to <see cref="JobStateEnum.Ready"/>
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