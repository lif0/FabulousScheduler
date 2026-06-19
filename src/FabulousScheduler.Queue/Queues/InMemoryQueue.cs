using System.Threading.Channels;
using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Queues;

public sealed class InMemoryQueue: IQueue
{
	#region Private

	private readonly Channel<IQueueJob> _channel;

	#endregion

	/// <summary>Number of buffered jobs (waiting consumers are not counted).</summary>
	public int Count => _channel.Reader.Count;

	public InMemoryQueue(int? capacity = null)
	{
		// Unbounded: Enqueue never blocks or fails. `capacity` is kept for source compatibility
		// and is only a hint, not a hard limit.
		_ = capacity;
		_channel = Channel.CreateUnbounded<IQueueJob>(new UnboundedChannelOptions
		{
			SingleWriter = false, // Enqueue can be called concurrently
			SingleReader = false  // many consumers, and >1 read may be pending at once
		});
	}

	public void Enqueue(IQueueJob job) => _channel.Writer.TryWrite(job);

	public void Enqueue(IEnumerable<IQueueJob> jobs)
	{
		foreach (var job in jobs)
		{
			_channel.Writer.TryWrite(job);
		}
	}

	public ValueTask<IQueueJob> NextAsync(CancellationToken cancellationToken = default)
		=> _channel.Reader.ReadAsync(cancellationToken);
}
