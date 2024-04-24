namespace FabulousScheduler.Queue.Interfaces;

public interface IQueue
{
	public int Count { get; }

	/// <summary>
	/// Push job to Queue
	/// </summary>
	/// <param name="job"></param>
	public void Enqueue(IQueueJob job);

	/// <summary>
	/// Take job from queue or wait while a the job will be pushed
	/// </summary>
	/// <returns></returns>
	public Task<IQueueJob> NextAsync();
}