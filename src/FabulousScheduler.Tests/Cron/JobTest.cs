using System.Diagnostics;
using FabulousScheduler.Cron.Enums;

namespace Job.Core.Tests.Cron;

public class JobTest
{
	[Fact]
	public async void TestExecutingJob_FailWithException()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1, TimeSpan.Zero);
		var job = new Job_FailExp("690", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		await manager.RecheckJobs();

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);
	}
	
	[Fact]
	public async void TestExecutingJob_Fail()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1, TimeSpan.Zero);
		var job = new Job_Fail("690", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		await manager.RecheckJobs();

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);
		
		Assert.Null(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
	}
	
	[Fact]
	public async void TestExecutingJob_Ok()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1, TimeSpan.Zero);
		var job = new Job_Ok("690", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		await manager.RecheckJobs();

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
		
		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
	}

	[Fact]
	public void TestExecutingJobTwoTime_InSleepTimePeriod()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 10, TimeSpan.Zero);
		var job = new Job_Ok("690", TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		
		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(), 
			manager.RecheckJobs(), 
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs());
		
		

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
	}
	
	[Fact]
	public async void TestExecutingJobTwoTime_OutSleepTimePeriod()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 10, TimeSpan.Zero);
		var job = new Job_Ok("690", TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		
		await manager.RecheckJobs();
		await Task.Delay(2500);
		Task.WaitAll(
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs(),
			manager.RecheckJobs());


		Assert.Equal(2, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
	}
	
	[Fact]
	public async void TestExecutingJob()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1);
		var job = new Job_Fail("690", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);

		var sw = Stopwatch.StartNew();
		await manager.RecheckJobs();
		sw.Stop();

		double errorCalc = 15;
		
		Assert.InRange(sw.Elapsed.TotalMilliseconds, oneTimeJob_ms-errorCalc, oneTimeJob_ms+errorCalc);
	}
	
	[Fact]
	public async void TestExecutingJobState_ToReady()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1, TimeSpan.Zero);
		var job = new Job_Fail("690", TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		await manager.RecheckJobs();

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);

		Assert.Null(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}
	
	[Fact]
	public async void TestExecutingJobState_ToSleep()
	{
		int oneTimeJob_ms = 25;

		var manager = new JobManager(maxParallelJob: 1, TimeSpan.Zero);
		var job = new Job_Ok("690", TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(oneTimeJob_ms));
		manager.Register(job);
		await manager.RecheckJobs();

		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.NotNull(job.LastSuccessExecute);
		Assert.NotNull(job.LastExecute);
		Assert.Equal(CronJobStateEnum.Sleeping, job.State);
	}
}