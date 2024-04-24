// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable ClassNeverInstantiated.Global
using FabulousScheduler.Cron;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Result;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Abstraction;

namespace Job.Core.Tests.Cron;

internal static class Helper
{
	public static double GuessDurationInMilliseconds(int countJobs, int parallelCount, double oneJobExecutionInMilliseconds)
	{
		parallelCount = (countJobs >= parallelCount ? parallelCount : 1);

		// ReSharper disable once PossibleLossOfFraction
		return countJobs / parallelCount * oneJobExecutionInMilliseconds;
	}

	public static async Task Sleep(CancellationToken cancellationToken)
	{
		try
		{
			await Task.Delay(-1, cancellationToken);
		}
		catch (Exception)
		{
			// ignored
		}
	}

	public static ulong SumUlong<TSource>(
		this IEnumerable<TSource> source,
		Func<TSource, ulong> selector)
	{
		ulong res = 0u;
		
		foreach (var s in source)
		{
			res += selector.Invoke(s);
		}
		
		return res;
	}
}

internal class CronJobRandomResult : BaseCronJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public CronJobRandomResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test random result", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);

		if (Random.Shared.Next(1, 10) % 2 == 0)
		{
			return new JobOk(this.ID);
		}

		return new JobFail(CronJobFailEnum.FailedExecute, this.ID, "test error");
	}
}

internal class CronJobOkResult : BaseCronJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public CronJobOkResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test success", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobOk(this.ID);
	}

}

internal class CronJobFailedExecuteResult : BaseCronJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public CronJobFailedExecuteResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test error", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobFail(CronJobFailEnum.FailedExecute, this.ID, "test error");
	}

	
}

internal class CronJobInternalExceptionResult : BaseCronJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public CronJobInternalExceptionResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name, "test error", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		throw new Exception("some internal exp");
	}

}

internal class TestCronScheduler : BaseCronScheduler
{
	public TestCronScheduler(Config? config) : base(config) { }

	public new bool Register(ICronJob job)
	{
		return base.Register(job);
	}

	public new int Register(IEnumerable<ICronJob> jobs)
	{
		return base.Register(jobs);
	}
}