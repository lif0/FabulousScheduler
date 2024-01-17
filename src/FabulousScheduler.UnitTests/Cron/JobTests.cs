using System.Diagnostics;
using FabulousScheduler.Cron.Enums;

namespace Job.Core.Tests.Cron;

public class JobTests
{
	[Fact]
	public async void Fail_FailedExecute()
	{
		var job = new Job_Fail("failExecuteJob", TimeSpan.Zero, TimeSpan.FromMilliseconds(25));
		var res = await job.ExecuteAsync();

		Assert.NotNull(job.LastExecute);
		Assert.Null(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);

		Assert.Equal(CronJobFailEnum.FailedExecute,  res.GetFail()!.Reason);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	public async void Fail_InternalException()
	{
		var job = new Job_FailExp("failInternalExceptionJob", TimeSpan.Zero, TimeSpan.FromMilliseconds(25));
		var res = await job.ExecuteAsync();

		Assert.NotNull(job.LastExecute);
		Assert.Null(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);

		Assert.Equal(CronJobFailEnum.InternalException,  res.GetFail()!.Reason);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}
	
	[Fact]
	public async void Fail_IncorrectState()
	{
		var job = new Job_Ok("ok_butFailIncorrectStateJob", TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(20));
		await job.ExecuteAsync(); //stat to sleep
		
		var res = await job.ExecuteAsync();

		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobFailEnum.IncorrectState,  res.GetFail()!.Reason);
		Assert.Equal(CronJobStateEnum.Sleeping, job.State);
	}
	
	[Fact]
	public async void Fail_Disposed()
	{
		var job = new Job_Ok("ok_butFailDisposedJob", TimeSpan.Zero, TimeSpan.FromMilliseconds(20));
		await job.DisposeAsync();
		var res = await job.ExecuteAsync();

		Assert.Null(job.LastExecute);
		Assert.Null(job.LastSuccessExecute);
		Assert.Equal(0, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobFailEnum.Disposed,  res.GetFail()!.Reason);
		Assert.Equal(CronJobStateEnum.Disposed, job.State);
	}

	[Fact]
	public async void Success_ChangeStateToSleep()
	{
		const int oneTimeJobMs = 25;
		
		var job = new Job_Ok("stateToSleep", TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		
		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobStateEnum.Sleeping, job.State);
	}

	[Fact]
	public async void Success_ChangeToReady()
	{
		const int oneTimeJobMs = 25;

		var job = new Job_Ok("stateToReady", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		
		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	public async void Success_SleepDuration_Max()
	{
		const int oneTimeJobMs = 25;

		var job = new Job_Ok("stateToReady", TimeSpan.MinValue, TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		
		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	public async void Success_SleepDuration_Min()
	{
		const int oneTimeJobMs = 25;

		var job = new Job_Ok("stateToReady", TimeSpan.MinValue, TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		
		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	public async void Success_SleepDuration_Zero()
	{
		const int oneTimeJobMs = 25;

		var job = new Job_Ok("stateToReady", TimeSpan.Zero, TimeSpan.FromMilliseconds(oneTimeJobMs));
		await job.ExecuteAsync();
		
		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);

		Assert.Equal(CronJobStateEnum.Ready, job.State);
	}

	[Fact]
	public async void Fail_CheckSpeed()
	{
		const int oneTimeJobMs = 20;
		var job = new Job_Fail("jobFail", TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(oneTimeJobMs));

		var sw = Stopwatch.StartNew();
		await job.ExecuteAsync();
		sw.Stop();
		
		Assert.NotNull(job.LastExecute);
		Assert.Null(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(1, job.TotalFail);
		Assert.Equal(CronJobStateEnum.Ready, job.State);
		
		Assert.Equal(oneTimeJobMs, sw.Elapsed.TotalMilliseconds, 5.0f );
	}
	
	[Fact]
	public async void Success_CheckSpeed()
	{
		const int oneTimeJobMs = 20;
		var job = new Job_Ok("jobSuccess", TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(oneTimeJobMs));

		var sw = Stopwatch.StartNew();
		await job.ExecuteAsync();
		sw.Stop();

		Assert.NotNull(job.LastExecute);
		Assert.NotNull(job.LastSuccessExecute);
		Assert.Equal(1, job.TotalRun);
		Assert.Equal(0, job.TotalFail);
		Assert.Equal(CronJobStateEnum.Sleeping, job.State);
		
		Assert.Equal(oneTimeJobMs, sw.Elapsed.TotalMilliseconds, 5.0f );
	}
}