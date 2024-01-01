namespace JobScheduler.Queue.Enums;

[Flags]
public enum QueueJobStateEnum
{
	/// <summary>
	/// The job is in the queue to the Executor.
	/// </summary>
	Waiting,

	/// <summary>
	/// The job is executing on the Executor.
	/// </summary>
	Running,
	
	/// <summary>
	/// The job is completed.
	/// </summary>
	Completed
}