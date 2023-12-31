using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Queues;
using Xunit.Abstractions;

namespace Job.Core.Tests.Queue;
/*
public class JobTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	public JobTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public async void JobTest_WaitFinishJob()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(1));
		var queue = new MemoryQueue();
		const int oneTimeJobMs = 2000;
		
		var manager = new QueueJobManager(queue, conf);
		
		var job = new QJob_FailExp("jobException", TimeSpan.FromMilliseconds(oneTimeJobMs));
		queue.Enqueue(job);

		manager.Start();
		await manager.FinishAllAndStopAsync();
		

		Assert.Equal(1, job.TotalRun);
		Assert.NotNull(job.LastExecute);
	}
	
	[Fact]
	public void JobTest_Stop()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(5));
		var queue = new MemoryQueue();
		const int oneTimeJobMs = 2000;
		
		var manager = new QueueJobManager(queue, conf);
		
		var job = new QJob_FailExp("jobException", TimeSpan.FromMilliseconds(oneTimeJobMs));
		queue.Enqueue(job);

		manager.Start();
		manager.Stop();
		

		Assert.Equal(0, job.TotalRun);
		Assert.Null(job.LastExecute);
	}
	
	[Fact]
	public void JobTest_StartStopEmptyProcessing()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(1));
		var queue = new MemoryQueue();
		
		var manager = new QueueJobManager(queue, conf);

		manager.Start();
		manager.Stop();
		
		Assert.True(true);
	}
	
	[Fact]
	public async void JobTest_ReAddToQueueProcessing()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(1));
		var queue = new MemoryQueue();
		const int oneTimeJobMs = 2000;
		
		var manager = new TestQueueReAddFailJobManager(queue, conf, 3);
		
		var job = new QJob_FailExp("jobException", TimeSpan.FromMilliseconds(oneTimeJobMs));
		queue.Enqueue(job);
		
		manager.Start();
		await manager.FinishAllAndStopAsync();


		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, queue.Count);
		Assert.NotNull(job.LastExecute);
		
	}
	
	[Fact]
	public async void JobTest_FinishAfterReAddToQueueProcessing()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(0.1));
		var queue = new MemoryQueue();
		const int oneTimeJobMs = 500;
		
		var manager = new TestQueueReAddFailJobManager(queue, conf, 3);
		
		var job = new QJob_Fail("jobFail", TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		queue.Enqueue(job);
		
		manager.Start();
		await Task.Delay(900);
		await manager.FinishAllAndStopAsync();


		Assert.Equal(2, job.TotalRun);
		Assert.Equal(1, queue.Count);
		Assert.NotNull(job.LastExecute);
		
	}
	
	[Fact]
	public async void JobTest_MaxAttemptReAddToQueueProcessing()
	{
		var conf = new Config(1, TimeSpan.FromSeconds(0.2));
		var queue = new MemoryQueue();
		const int oneTimeJobMs = 200;
		
		var manager = new TestQueueReAddFailJobManager(queue, conf, 3);
		
		var job = new QJob_Fail("jobFail", TimeSpan.FromMilliseconds(oneTimeJobMs));
		queue.Enqueue(job);
		
		manager.Start();
		await Task.Delay(2000);
		await manager.FinishAllAndStopAsync();


		Assert.Equal(3, job.TotalRun);
		Assert.Equal(0, queue.Count);
		Assert.NotNull(job.LastExecute);
		
	}
}
*/