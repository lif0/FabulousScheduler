using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Result;
using System.Collections.Concurrent;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron;
using System.Diagnostics;


namespace Job.Core.Tests.Cron;
public class JobManagerTests
{
	[Fact]
	public async void Time_FailOne()
	{
		const int oneTimeJobMs = 50;
		
		// helper
		int countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var config = new Config( maxParallelJobExecute: 1, sleepAfterCheck: TimeSpan.FromHours(1));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> _) =>
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		};
		
		// test
		var job = new Job_Fail("okFail", sleepDuration: TimeSpan.Zero, jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
		sw.Start();

		await tcs.Task;
		
		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);
		Assert.Null(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}
	
	[Fact]
	public async void Time_SuccessOne()
	{
		const int oneTimeJobMs = 50;
		
		// helper
		int countCall = 0;
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var config = new Config( maxParallelJobExecute: 1, sleepAfterCheck: TimeSpan.FromHours(1));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> _) =>
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		};
		
		// test
		var job = new Job_Ok("okJob", sleepDuration: TimeSpan.Zero, jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
		sw.Start();

		await tcs.Task;
		
		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}
	
	[Fact]
	public async void Time_1k()
	{
		int countJobs = 1000, oneTimeJobMs = 20, parallelJobs = Environment.ProcessorCount*5;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(50));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.ID, _ => 1, (_, b) => ++b);

			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		};
		
		// test
		var jobs = new List<Job_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		sw.Start();

		await tcs.Task;
		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		long countCall = jobs.Sum(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilleseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, uniqCountCall);
		Assert.Equal(countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,300f/*0.3 of sec*/);
	}
	
	[Fact]
	public async void Time_5k()
	{
		int countJobs = 5000, oneTimeJobMs = 20, parallelJobs = Environment.ProcessorCount*5;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(50));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.ID, _ => 1, (_, b) => ++b);

			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		};
		
		// test
		var jobs = new List<Job_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		sw.Start();

		await tcs.Task;
		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		long countCall = jobs.Sum(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilleseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, uniqCountCall);
		Assert.Equal(countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,1000f/*1 sec*/);
	}
	
	[Fact]
	public async void Time_50k()
	{
		int countJobs = 50000, oneTimeJobMs = 5, parallelJobs = Environment.ProcessorCount*10;

		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();

		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(50));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.ID, _ => 1, (_, b) => ++b);

			if (hash.Count == countJobs)
			{
				sw.Stop();
				tcs.SetResult();
			}
		};
		
		// test
		var jobs = new List<Job_Random>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		sw.Start();

		await tcs.Task;
		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		long countCall = jobs.Sum(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilleseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, uniqCountCall);
		Assert.Equal(countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,1000f/*1 sec*/);
	}

	[Fact]
	public async void Count_InSleepPeriod()
	{
		int countJobs = 1000, oneTimeJobMs = 5, parallelJobs = Environment.ProcessorCount*10;

		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		CancellationTokenSource cts = new();

		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(10));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.ID, _ => 1, (_, b) => ++b);
		};

		// test
		var jobs = new List<Job_Ok>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Ok(i.ToString(), TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		cts.CancelAfter(TimeSpan.FromSeconds(3));
		await Helper.Sleep(cts.Token);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		
		Assert.Equal(countJobs, hash.Count);
		Assert.Equal(countJobs, hash.Sum(x=> x.Value));
		Assert.Equal(countJobs, jobs.Sum(x => x.TotalRun));
		Assert.True(allHasLastExecute);
	}
	
	[Fact]
	public async void Count_OutSleepPeriod()
	{
		int countJobs = 1000, oneTimeJobMs = 5, sleepDurationSec = 1, parallelJobs = Environment.ProcessorCount*10;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		CancellationTokenSource cts = new();

		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(10));
		var manager = new TestCronJobScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.ID, _ => 1, (_, b) => ++b);
		};

		// test
		var jobs = new List<Job_Ok>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Ok(i.ToString(), TimeSpan.FromSeconds(sleepDurationSec), TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		// ReSharper disable once UselessBinaryOperation
		cts.CancelAfter(TimeSpan.FromSeconds(sleepDurationSec*2));
		await Helper.Sleep(cts.Token);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		
		Assert.Equal(countJobs, hash.Count);
		Assert.Equal(countJobs*2, hash.Sum(x=> x.Value));
		Assert.Equal(countJobs*2, jobs.Sum(x => x.TotalRun));
		Assert.True(allHasLastExecute);
	}
}