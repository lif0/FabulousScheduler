namespace FabulousScheduler.Queue.Interfaces;

public interface IQueue
{
	public int Count { get; }
	
	public void Enqueue(IQueueJob job);
	public IQueueJob? TryDequeue();
}