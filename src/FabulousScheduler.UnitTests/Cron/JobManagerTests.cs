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
		var scheduler = new TestCronScheduler(config);
		void ManagerOnJobResultEvent(ref ICronJob sender, ref JobResult<JobOk, JobFail> e)
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;

		// test
		var job = new Job_Fail("okFail", sleepDuration: TimeSpan.Zero, jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs));
		var registered = scheduler.Register(job);
		scheduler.RunScheduler();
		sw.Start();

		await tcs.Task;

		Assert.True(registered);
		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.Equal(1u, job.TotalFail);
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
		var config = new Config(maxParallelJobExecute: 1, sleepAfterCheck: TimeSpan.FromHours(1));
		var scheduler = new TestCronScheduler(config);

		void ManagerOnJobResultEvent(ref ICronJob sender, ref JobResult<JobOk, JobFail> e)
		{
			sw.Stop();
			Interlocked.Increment(ref countCall);
			tcs.SetResult();
			
		}
		scheduler.JobResultEvent += ManagerOnJobResultEvent;
		
		// test
		var job = new Job_Ok("okJob", sleepDuration: TimeSpan.Zero, jobSimulateWorkTime: TimeSpan.FromMilliseconds(oneTimeJobMs));
		var registered = scheduler.Register(job);
		Assert.True(registered);
		scheduler.RunScheduler();
		sw.Start();

		await tcs.Task;
		scheduler.JobResultEvent -= ManagerOnJobResultEvent;

		Assert.Equal(1, countCall);
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.Equal(1u, job.TotalRun);
		Assert.Equal(0u, job.TotalFail);
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
		var manager = new TestCronScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.JobID, _ => 1, (_, b) => ++b);
	
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
		manager.RunScheduler();
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
		int countJobs = 5000, oneTimeJobMs = 20, parallelJobs = Environment.ProcessorCount*5;
		
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
	
		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(50));
		var manager = new TestCronScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.JobID, _ => 1, (_, b) => ++b);
	
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
		manager.RunScheduler();
		sw.Start();
	
		await tcs.Task;

		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		ulong countCall = jobs.SumUlong(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilliseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, (ulong)uniqCountCall);
		Assert.Equal((ulong)countJobs, countCall);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,5000f/*5 sec*/);
	}
	
	[Fact]
	public async void Time_50k()
	{
		int countJobs = 50000, oneTimeJobMs = 3, parallelJobs = Environment.ProcessorCount*20;
	
		// helper
		var hash = new ConcurrentDictionary<Guid, byte>();
		TaskCompletionSource tcs = new();
		Stopwatch sw = new Stopwatch();
	
		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(50));
		var manager = new TestCronScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.JobID, _ => 1, (_, b) => ++b);
	
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
		manager.RunScheduler();
		sw.Start();
	
		await tcs.Task;

		long uniqCountCall = jobs.Count(x => x.TotalRun == 1);
		ulong countCall = jobs.SumUlong(x => x.TotalRun);
		double expectedWorkTimeSec = Helper.GuessDurationInMilliseconds(countJobs, parallelJobs, oneTimeJobMs);
		
		Assert.Equal(countCall, (ulong)uniqCountCall);
		Assert.Equal((ulong)countJobs, countCall);
		//Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalMilliseconds,1000f/*1 sec*/);
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
		var manager = new TestCronScheduler(config);
		manager.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.JobID, _ => 1, (_, b) => ++b);
		};
	
		// test
		var jobs = new List<Job_Ok>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Ok(i.ToString(), TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		manager.Register(jobs);
		manager.RunScheduler();
		cts.CancelAfter(TimeSpan.FromSeconds(3));
		await Helper.Sleep(cts.Token);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		
		Assert.Equal(countJobs, hash.Count);
		Assert.Equal(countJobs, hash.Sum(x=> x.Value));
		Assert.Equal((ulong)countJobs, jobs.SumUlong(x => x.TotalRun));
		Assert.True(allHasLastExecute);
	}
	
	[Fact] // TODO KGG:> есть ощущение что тест соствлен неверно, тут нужно убедить что каждая задача запускается в верном промежутке, а то что написанно сейчас, тестирует не понятно что
	public async void Count_OutSleepPeriod()
	{
		int countJobs = 1000, oneTimeJobMs = 5, sleepDurationSec = 1, parallelJobs = Environment.ProcessorCount*10;

		// helper
		long countCall = 0;
		var hash = new ConcurrentDictionary<Guid, byte>();
		CancellationTokenSource cts = new();
	
		// init
		var config = new Config( maxParallelJobExecute: parallelJobs, sleepAfterCheck: TimeSpan.FromMilliseconds(20) );
		var scheduler = new TestCronScheduler(config);
		scheduler.JobResultEvent += (ref ICronJob _, ref JobResult<JobOk, JobFail> e) =>
		{
			hash.AddOrUpdate(e.JobID, _ => 1, (_, b) => ++b);
			Interlocked.Increment(ref countCall);
			if (Interlocked.Read(ref countCall) == countJobs*2)
			{
				cts.Cancel();
			}
		};
	
		// test
		var jobs = new List<Job_Ok>(countJobs);
		for (int i = 1; i <= countJobs; i++)
		{
			jobs.Add(new Job_Ok(i.ToString(), TimeSpan.FromSeconds(sleepDurationSec), TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		scheduler.Register(jobs);
		scheduler.RunScheduler();

		await Helper.Sleep(cts.Token);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		
		Assert.Equal(countJobs, hash.Count);
		Assert.Equal(countJobs*2, hash.Sum(x=> x.Value));
		Assert.Equal((ulong)(countJobs*2u), jobs.SumUlong(x => x.TotalRun));
		Assert.True(allHasLastExecute);
	}
}