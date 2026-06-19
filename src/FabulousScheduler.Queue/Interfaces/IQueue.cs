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
	/// Take a job from the queue, or wait until one is pushed.
	/// </summary>
	/// <param name="cancellationToken">Cancels the wait (e.g. on scheduler shutdown).</param>
	/// <returns>The next job.</returns>
	public ValueTask<IQueueJob> NextAsync(CancellationToken cancellationToken = default);
}