using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Cron;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Interfaces;

namespace Job.Core.Tests.Cron;

public class JobManagerTests
{
	[Fact]
	public async void Time_FailOne()
	{
		const int oneTimeJobMs = 100;
	
		var manager = new CronJobManagerManualRecheck(new Config(1, TimeSpan.MaxValue));
		var job = new Job_Fail("okFail", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
	
		var sw = Stopwatch.StartNew();
		var results = await manager.RecheckJobs();
		sw.Stop();
	
		Assert.Single(results);
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
		const int oneTimeJobMs = 100;
	
		var manager = new CronJobManagerManualRecheck(new Config(1, TimeSpan.MaxValue));
		var job = new Job_Ok("okJob", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
	
		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();
	
		Assert.Equal(oneTimeJobMs,sw.Elapsed.TotalMilliseconds, 10.0f);
		
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	[SuppressMessage("ReSharper", "PossibleLossOfFraction")]
	public async void Time_1k()
	{
		int countJobs = 1000, oneTimeJobMs = 20, parallelJobs = 10;
		var manager = new CronJobManagerManualRecheck(new Config(parallelJobs, TimeSpan.MaxValue));

		for (int i = 1; i <= countJobs; i++)
		{
			manager.Register(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}

		var sw = Stopwatch.StartNew();
		var results = await manager.RecheckJobs();
		sw.Stop();
		
		double expectedWorkTimeSec = countJobs / (countJobs >= parallelJobs ? parallelJobs : 1) *
		                             (oneTimeJobMs / 1000.0 /*in sec*/);
		Assert.Equal(expectedWorkTimeSec,sw.Elapsed.TotalSeconds,5f);
		Assert.Equal(countJobs, results.Length);
	}

	[Fact]
	public async void Time_5k()
	{
		int countJobs = 5000, oneTimeJobMs = 6, parallelJobs = 20;
		var manager = new CronJobManagerManualRecheck(new Config(parallelJobs, TimeSpan.MaxValue));
		
		
		for (int i = 1; i <= countJobs; i++)
		{
			manager.Register(new Job_Random(i.ToString(), TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		
		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();
		
		
		
		// ReSharper disable once PossibleLossOfFraction
		double expectedWorkTimeSec = countJobs / (countJobs >= parallelJobs ? parallelJobs : 1) * 
		                             (oneTimeJobMs / 1000.0 /*in sec*/);
		Assert.Equal(expectedWorkTimeSec, sw.Elapsed.TotalSeconds, 5f);
	}
	
	[Fact]
	public async void Time_50k()
	{
		int countJobs = 50000, oneTimeJobMs = 5, parallelJobs = 100;
		var manager = new CronJobManagerManualRecheck(new Config(parallelJobs, TimeSpan.MaxValue));

		for (int i = 1; i <= countJobs; i++)
		{
			manager.Register(new Job_Random(i.ToString(), TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs)));
		}
		
		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();

		// ReSharper disable once PossibleLossOfFraction
		double expectedWorkTimeSec = countJobs / (countJobs >= parallelJobs ? parallelJobs : 1) * 
		                             (oneTimeJobMs / 1000.0 /*in sec*/);
		Assert.Equal(expectedWorkTimeSec, sw.Elapsed.TotalSeconds, 5f);
	}
	
	[Fact]
	public void Count_InSleepPeriod()
	{
		int countJobs = 10000, oneTimeJobMs = 1, parallelJobs = 10;
		var manager = new CronJobManagerManualRecheck(new Config(parallelJobs, TimeSpan.MaxValue));

		var jobs = new List<ICronJob>();
		
		for (int i = 1; i <= countJobs; i++)
		{
			var job = new Job_Ok(i.ToString(), TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(oneTimeJobMs));
			jobs.Add(job);
			
		}
		manager.Register(jobs);
		
		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs()
		);

		double actual = jobs.Sum(x => x.TotalRun);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		Assert.Equal(countJobs, actual);
		Assert.True(allHasLastExecute);
	}

	[Fact]
	public async void Count_OutSleepPeriod()
	{
		TimeSpan sleepDuration = TimeSpan.FromSeconds(1);
		int countJobs = 10000, oneTimeJobMs = 1, parallelJobs = 10;
		var manager = new CronJobManagerManualRecheck(new Config(parallelJobs, TimeSpan.MaxValue));

		var jobs = new List<ICronJob>();
		
		for (int i = 1; i <= countJobs; i++)
		{
			var job = new Job_Ok(i.ToString(), sleepDuration, TimeSpan.FromMilliseconds(oneTimeJobMs));
			jobs.Add(job);
			
		}
		manager.Register(jobs);

		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs()
		);
		await Task.Delay(sleepDuration*2);
		await manager.RecheckJobs();


		double actual = jobs.Sum(x => x.TotalRun);
		bool allHasLastExecute = jobs.TrueForAll(x => x.LastSuccessExecute.HasValue);
		Assert.Equal(countJobs*2, actual);
		Assert.True(allHasLastExecute);
	}

	[Fact]
	public void Count_JobTwoTimeInSleepTimePeriod()
	{
		const int oneTimeJobMs = 25;
		var manager = new JobManager(new Config(10, TimeSpan.MaxValue));
		var job = new Job_Ok("jobOk", TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
		
		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(), 
			manager.RecheckJobs(), 
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs()
		);
		
		

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
	}

	[Fact]
	public async void Count_TwoTimeOutSleepTimePeriod()
	{
		const int oneTimeJobMs = 25;

		var manager = new JobManager(new Config(10, TimeSpan.MaxValue));
		var job = new Job_Ok("jobOk", TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(oneTimeJobMs));
		manager.Register(job);
		
		await manager.RecheckJobs();
		await Task.Delay(2500);
		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs()
		);


		Assert.Equal(2, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
	}
}