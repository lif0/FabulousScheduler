using System.Diagnostics;
using Job.Cron.Interfaces;

namespace Job.Core.Tests.Cron;

public class ParallelExecutingJobsTest
{
	[Fact]
	public async void TestExecutingJob()
	{
		int oneTimeJob_ms = 25;

		var manager = new CronJobManagerManualRecheck(maxParallelJob: 1);
		var job = new Job_Fail("690", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);

		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();

		double errorCalc = 15;
		
		Assert.InRange(sw.Elapsed.TotalMilliseconds, oneTimeJob_ms-errorCalc, oneTimeJob_ms+errorCalc);
	}
	
	[Fact]
	public async void TestParallelExecutingJobs_5k()
	{
		int countJobs = 5000, oneTimeJob_ms = 25, parallelJobs = 50;
		var manager = new CronJobManagerManualRecheck(maxParallelJob: parallelJobs);
		
		
		for (int i = 1; i <= countJobs; i++)
		{
			manager.Register(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJob_ms)));
		}

		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();
		
		
		double shouldWorkSecond = (countJobs / (countJobs >= parallelJobs ? parallelJobs : 1)) * (oneTimeJob_ms / 1000.0 /*in sec*/);
		double errorCalc = 0.5;
		
		Assert.InRange(sw.Elapsed.TotalSeconds, shouldWorkSecond-errorCalc, shouldWorkSecond+errorCalc);
	}
	
	[Fact]
	public async void TestParallelExecutingJobs_50k()
	{
		int countJobs = 50000, oneTimeJob_ms = 5, parallelJobs = 100;
		var manager = new CronJobManagerManualRecheck(maxParallelJob: parallelJobs);
		
		
		for (int i = 1; i <= countJobs; i++)
		{
			manager.Register(new Job_Random(i.ToString(), TimeSpan.MaxValue, TimeSpan.FromMilliseconds(oneTimeJob_ms)));
		}

		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();
		
		
		
		// ReSharper disable once PossibleLossOfFraction
		double shouldWorkSecond = countJobs / (countJobs >= parallelJobs ? parallelJobs : 1) * 
		                          (oneTimeJob_ms / 1000.0 /*in sec*/);
		double errorCalc = 0.9;
		
		Assert.InRange(sw.Elapsed.TotalSeconds, shouldWorkSecond-errorCalc, shouldWorkSecond+errorCalc);
	}
	
	[Fact]
	public async void TestParallelExecutingJobs_InSleepPeriod()
	{
		int countJobs = 5000, oneTimeJob_ms = 20, parallelJobs = 50;
		var manager = new CronJobManagerManualRecheck(maxParallelJob: parallelJobs, TimeSpan.Zero);

		var jobs = new List<ICronJob>();
		
		for (int i = 1; i <= countJobs; i++)
		{
			var job = new Job_Ok(i.ToString(), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(oneTimeJob_ms));
			jobs.Add(job);
			
		}
		manager.Register(jobs);
		
		Task.WaitAll(manager.RecheckJobs(),manager.RecheckJobs(),manager.RecheckJobs(), manager.RecheckJobs());

		double actual = jobs.Sum(x => x.TotalRun);

		Assert.Equal(5000, actual);
	}
	
	[Fact]
	public async void TestParallelExecutingJobs_OutSleepPeriod()
	{
		int countJobs = 5000, oneTimeJob_ms = 20, parallelJobs = 50;
		var manager = new CronJobManagerManualRecheck(maxParallelJob: parallelJobs, TimeSpan.Zero);

		var jobs = new List<ICronJob>();
		
		for (int i = 1; i <= countJobs; i++)
		{
			var job = new Job_Ok(i.ToString(), TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(oneTimeJob_ms));
			jobs.Add(job);
			
		}
		manager.Register(jobs);


		Task.WaitAll(manager.RecheckJobs(),manager.RecheckJobs(),manager.RecheckJobs(), manager.RecheckJobs());
		await Task.Delay(1500);
		await manager.RecheckJobs();



		double actual = jobs.Sum(x => x.TotalRun);

		Assert.Equal(10000, actual);
	}
	
}