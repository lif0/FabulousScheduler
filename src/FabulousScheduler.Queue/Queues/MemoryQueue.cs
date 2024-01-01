using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Queues;

public class MemoryQueue: IQueue
{
	#region Private
	
	private readonly object _lock;
	private readonly Queue<IQueueJob> _queue;
	
	#endregion

	// ReSharper disable once InconsistentlySynchronizedField
	public int Count => _queue.Count;

	public MemoryQueue()
	{
		_lock = new object();
		_queue = new ();
	}

	public void Enqueue(IQueueJob job)
	{
		lock (_lock)
		{
			_queue.Enqueue(job);
		}
	}

	public IQueueJob? TryDequeue()
	{
		lock (_lock)
		{
			if (_queue.TryDequeue(out var res))
			{
				return res;
			}

			return null;
		}
	}
}