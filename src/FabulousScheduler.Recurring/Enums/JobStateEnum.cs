namespace FabulousScheduler.Recurring.Enums;

[Flags]
public enum JobStateEnum
{
	/// <summary>
	/// The job is ready to run and waiting for the Executor.
	/// </summary>
	Ready,
	
	/// <summary>
	/// The job is in the queue to the Executor.
	/// </summary>
	Waiting,

	/// <summary>
	/// The job is executing on the Executor.
	/// </summary>
	Running,
	
	/// <summary>
	/// The job has been told to sleep for a time.
	/// </summary>
	Sleeping,
	
	/// <summary>
	/// The job was disposed
	/// </summary>
	Disposed,
}