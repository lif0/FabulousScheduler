using System.Collections.Concurrent;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;

namespace Job.Core.Tests.Queue;

public class InMemoryQueueTests
{
	private static IQueueJob NewJob(string name = "job") =>
		new QueueJobOkResult(name, "test", isAsyncAction: true, TimeSpan.Zero);

	[Fact]
	public void Count_NewQueue_IsZero()
	{
		var queue = new InMemoryQueue();
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task Enqueue_Then_Next_ReturnsFifo()
	{
		var queue = new InMemoryQueue();
		var a = NewJob("a");
		var b = NewJob("b");
		var c = NewJob("c");

		queue.Enqueue(a);
		queue.Enqueue(b);
		queue.Enqueue(c);
		Assert.Equal(3, queue.Count);

		Assert.Equal(a.ID, (await queue.NextAsync()).ID);
		Assert.Equal(2, queue.Count);
		Assert.Equal(b.ID, (await queue.NextAsync()).ID);
		Assert.Equal(1, queue.Count);
		Assert.Equal(c.ID, (await queue.NextAsync()).ID);
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task NextAsync_OnEmpty_CompletesAfterEnqueue()
	{
		var queue = new InMemoryQueue();

		var pending = queue.NextAsync();
		Assert.False(pending.IsCompleted);
		// a waiting consumer is not buffered work, so Count stays 0
		Assert.Equal(0, queue.Count);

		var job = NewJob();
		queue.Enqueue(job);

		var result = await pending;
		Assert.Equal(job.ID, result.ID);
		// the job went straight to the waiter, nothing was buffered
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task NextAsync_MultipleWaiters_SatisfiedInFifoOrder()
	{
		var queue = new InMemoryQueue();

		var first = queue.NextAsync();
		var second = queue.NextAsync();
		Assert.False(first.IsCompleted);
		Assert.False(second.IsCompleted);

		var a = NewJob("a");
		var b = NewJob("b");
		queue.Enqueue(a);
		queue.Enqueue(b);

		Assert.Equal(a.ID, (await first).ID);
		Assert.Equal(b.ID, (await second).ID);
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task EnqueueBatch_NoWaiters_BuffersAllInOrder()
	{
		var queue = new InMemoryQueue();
		var jobs = new[] { NewJob("a"), NewJob("b"), NewJob("c") };

		queue.Enqueue(jobs);
		Assert.Equal(3, queue.Count);

		foreach (var expected in jobs)
		{
			Assert.Equal(expected.ID, (await queue.NextAsync()).ID);
		}
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task EnqueueBatch_WithWaiters_SatisfiesWaitersThenBuffersRest()
	{
		var queue = new InMemoryQueue();

		// two waiting consumers
		var first = queue.NextAsync();
		var second = queue.NextAsync();

		var a = NewJob("a");
		var b = NewJob("b");
		var c = NewJob("c");
		var d = NewJob("d");
		queue.Enqueue(new[] { a, b, c, d });

		// the first two jobs satisfy the two waiters in order
		Assert.Equal(a.ID, (await first).ID);
		Assert.Equal(b.ID, (await second).ID);

		// the remaining two are buffered, still in order
		Assert.Equal(2, queue.Count);
		Assert.Equal(c.ID, (await queue.NextAsync()).ID);
		Assert.Equal(d.ID, (await queue.NextAsync()).ID);
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task EnqueueBatch_MoreWaitersThanJobs_LeavesWaiterPending()
	{
		var queue = new InMemoryQueue();

		var first = queue.NextAsync();
		var second = queue.NextAsync();
		var third = queue.NextAsync();

		var a = NewJob("a");
		var b = NewJob("b");
		queue.Enqueue(new[] { a, b });

		Assert.Equal(a.ID, (await first).ID);
		Assert.Equal(b.ID, (await second).ID);
		Assert.False(third.IsCompleted);
		Assert.Equal(0, queue.Count);

		// the still-waiting consumer is served by the next enqueue
		var c = NewJob("c");
		queue.Enqueue(c);
		Assert.Equal(c.ID, (await third).ID);
		Assert.Equal(0, queue.Count);
	}

	[Fact]
	public async Task ConcurrentProducersConsumers_DeliverEachJobExactlyOnce()
	{
		const int total = 1000;
		var queue = new InMemoryQueue(total);

		var jobs = Enumerable.Range(0, total).Select(i => NewJob(i.ToString())).ToArray();
		var expectedIds = jobs.Select(x => x.ID).ToHashSet();

		var received = new ConcurrentBag<Guid>();
		var consumers = Enumerable.Range(0, total)
			.Select(_ => Task.Run(async () =>
			{
				var job = await queue.NextAsync();
				received.Add(job.ID);
			}))
			.ToArray();

		Parallel.ForEach(jobs, job => queue.Enqueue(job));

		await Task.WhenAll(consumers);

		Assert.Equal(total, received.Count);
		Assert.True(expectedIds.SetEquals(received));
		Assert.Equal(0, queue.Count);
	}
}
