using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Queue.Enums;
using System.Collections.Concurrent;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;
using Job.Core.Tests.Cron;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Job.Core.Tests.Queue;

public class JobManagerTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	public JobManagerTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public async void Time_FailOne()
	{
		const int oneTimeJobMs = 500;
		
		// helper
		int countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var queue = new InMemoryQueue();
		var config = new Configuration( maxParallelJobExecute: 1);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var job = new QJob_Fail(
			name: "jobFail",
			category: "test",
			isAsyncAction: true,
			jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
		);
		queue.Enqueue(job);
		scheduler.RunScheduler();
		sw.Start();
		
		await tcs.Task;

		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.Null(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(QueueJobStateEnum.Completed, job.State);
	}
	
	[Fact]
	public async void Time_FailExpOne()
	{
		const int oneTimeJobMs = 500;
		
		// helper
		int countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var queue = new InMemoryQueue();
		var config = new Configuration( maxParallelJobExecute: 1);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var job = new QJob_FailExp(
			name: "jobFailExp",
			category: "test",
			isAsyncAction: true,
			jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
		);
		queue.Enqueue(job);
		scheduler.RunScheduler();
		sw.Start();
		
		await tcs.Task;

		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.Null(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(QueueJobStateEnum.Completed, job.State);
	}

	[Fact]
	public async void Time_SuccessOne()
	{
		const int oneTimeJobMs = 500;
		
		// helper
		int countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var queue = new InMemoryQueue();
		var config = new Configuration( maxParallelJobExecute: 1);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var job = new QJob_Ok(
			name: "jobSuccess",
			category: "test",
			isAsyncAction: true,
			jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
		);
		queue.Enqueue(job);
		scheduler.RunScheduler();
		sw.Start();
		
		await tcs.Task;

		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(QueueJobStateEnum.Completed, job.State);
	}
	
	[Fact]
	public async void Time_SuccessDuplicateJob()
	{
		const int oneTimeJobMs = 500;
	
		// helper
		long countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
		var okRes = new ConcurrentBag<JobResult<JobOk, JobFail>>();
		var failRes = new ConcurrentBag<JobResult<JobOk, JobFail>>();

		// init
		var queue = new InMemoryQueue();
		var config = new Configuration(maxParallelJobExecute: 1);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			if (result.IsSuccess)
			{
				okRes.Add(result);
			}
			else if(result.IsFail)
			{
				failRes.Add(result);
			}

			Interlocked.Increment(ref countCall);

			if (Interlocked.Read(ref countCall) == 2)
			{
				sw.Stop();
				tcs.SetResult();
			}
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent; 
		
		// test
		var job = new QJob_Ok(
			name: "job_Fail_Success",
			category: "test",
			isAsyncAction: true,
			jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
		);
		queue.Enqueue(job);
		queue.Enqueue(job);
		scheduler.RunScheduler();
		sw.Start();
		
		await tcs.Task;

		Assert.Equal(2, countCall);
		Assert.Single(okRes);
		Assert.Single(failRes);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		Assert.Equal(QueueJobFailEnum.IncorrectState, failRes.First().GetFail()!.Reason);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(QueueJobStateEnum.Completed, job.State);
	}
	
	[Fact]
	public async void Time_AttemptsFailNextOk()
	{
		const int oneTimeJobMs = 250;
		
		// helper
		long countCall = 0;
		TaskCompletionSource tcs = new();
		var okRes = new ConcurrentBag<JobResult<JobOk, JobFail>>();
		var failRes = new ConcurrentBag<JobResult<JobOk, JobFail>>();

		// init
		var queue = new InMemoryQueue();
		var config = new Configuration( maxParallelJobExecute: 1);
		var scheduler = new TestQueueSchedulerWithAttempts(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			if (result.IsSuccess)
			{
				okRes.Add(result);
			}
			else if(result.IsFail)
			{
				failRes.Add(result);
			}

			Interlocked.Increment(ref countCall);

			if (Interlocked.Read(ref countCall) == 2)
			{
				tcs.SetResult();
			}
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var job = new QueueJobAttemptsFailNextOk(
			name: "jobFailNextSuccess",
			category: "test",
			isAsyncAction: true,
			jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
		);
		queue.Enqueue(job);
		scheduler.RunScheduler();

		await tcs.Task;
		
		Assert.Equal(2, countCall);
		Assert.Single(okRes);
		Assert.Single(failRes);
		Assert.Equal(QueueJobFailEnum.InternalException, failRes.First().GetFail()!.Reason);

		Assert.Equal(2, countCall);
		Assert.Equal(2u, job.TotalRun);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(QueueJobStateEnum.Completed, job.State);
	}
 	
	[Fact]
	public async void Time_1k()
	{
		int countJobs = 1000, oneTimeJobMs = 50, parallelJobs = Environment.ProcessorCount*10;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
	
		// init
		var queue = new InMemoryQueue(countJobs);
		var config = new Configuration(maxParallelJobExecute: parallelJobs);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			hash.AddOrUpdate(result.JobID, _ => 1, (_, b) => ++b);
	
			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		}

		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var jobs = new List<QJob_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(
			new QJob_Random(
					name: i.ToString(),
					category: "test",
					isAsyncAction: true,
					jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs))
				);
		}
		queue.Enqueue(jobs);
		scheduler.RunScheduler();
		sw.Start();
	
		await tcs.Task;

		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		ulong countCall = jobs.SumUlong(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilliseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, (ulong)uniqCountCall);
		Assert.Equal((ulong)countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,300f/*0.3 of sec*/);
	}

	[Fact]
	public async void Time_5k()
	{
		int countJobs = 5000, oneTimeJobMs = 20, parallelJobs = Environment.ProcessorCount*10;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
	
		// init
		var queue = new InMemoryQueue(countJobs);
		var config = new Configuration(maxParallelJobExecute: parallelJobs);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			hash.AddOrUpdate(result.JobID, _ => 1, (_, b) => ++b);
	
			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		}

		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var jobs = new List<QJob_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(
			new QJob_Random(
					name: i.ToString(),
					category: "test",
					isAsyncAction: true,
					jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs)
				)
			);
		}
		queue.Enqueue(jobs);
		scheduler.RunScheduler();
		sw.Start();
	
		await tcs.Task;

		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		ulong countCall = jobs.SumUlong(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilliseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, (ulong)uniqCountCall);
		Assert.Equal((ulong)countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,300f/*0.3 of sec*/);
	}

/*	
    [Fact]
	public async void Time_50k()
	{
		int countJobs = 50000, oneTimeJobMs = 5, parallelJobs = Environment.ProcessorCount*10;
	
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
	
		// init
		var queue = new InMemoryQueue(countJobs);
		var config = new Config(maxParallelJobExecute: parallelJobs);
		var scheduler = new TestQueueScheduler(config, queue);
		void ManagerOnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> result)
		{
			hash.AddOrUpdate(result.JobID, _ => 1, (_, b) => ++b);
	
			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		}

		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var jobs = new List<QJob_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(
				new QJob_Random(
					name: i.ToString(),
					category: "test",
					isAsyncAction: true,
					jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs))
			);
		}
		queue.Enqueue(jobs);
		scheduler.RunScheduler();
		sw.Start();
	
		await tcs.Task;

		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		ulong countCall = jobs.SumUlong(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilliseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, (ulong)uniqCountCall);
		Assert.Equal((ulong)countJobs, countCall);
		//Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,300f);
	}
*/
}