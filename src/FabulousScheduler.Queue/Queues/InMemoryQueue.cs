using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Queues;

public sealed class InMemoryQueue: IQueue
{
	#region Private
	
	private readonly object _lock;
	private readonly Queue<IQueueJob> _queue;
	private readonly Queue<TaskCompletionSource<IQueueJob>> _rQueue;
	
	#endregion

	// ReSharper disable once InconsistentlySynchronizedField
	public int Count => _queue.Count;

	public InMemoryQueue(int? capacity = null)
	{
		_lock = new object();
		_queue = new ();

		_rQueue = capacity is not null ? 
			new Queue<TaskCompletionSource<IQueueJob>>(capacity.Value) :
			new Queue<TaskCompletionSource<IQueueJob>>();
	}

	public void Enqueue(IQueueJob job)
	{
		lock (_lock)
		{
			if (_rQueue.Count == 0)
			{
				_queue.Enqueue(job);
			}
			else
			{
				var callTask = _rQueue.Dequeue();
				callTask.SetResult(job);
			}
		}
	}
	
	public void Enqueue(IEnumerable<IQueueJob> jobs)
	{
		lock (_lock)
		{
			var take = _rQueue.Count;

			foreach (var j in jobs.Take(take))
			{
				var callTask = _rQueue.Dequeue();
				callTask.SetResult(j);
			}

			foreach (var j in jobs.Skip(take))
			{
				_queue.Enqueue(j);
			}
		}
	}

	public Task<IQueueJob> NextAsync()
	{
		lock (_lock)
		{
			if (_queue.Count == 0)
			{
				var tcs = new TaskCompletionSource<IQueueJob>();
				_rQueue.Enqueue(tcs);
				return tcs.Task;
			}
			
			return Task.FromResult(_queue.Dequeue());
		}
	}
}