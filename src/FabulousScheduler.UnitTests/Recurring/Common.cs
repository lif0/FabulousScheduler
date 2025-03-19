using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Recurring.Abstraction;
using FabulousScheduler.Recurring.Enums;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Recurring;
using FabulousScheduler.Recurring.Result;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace Job.Core.Tests.Recurring;

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
		catch (Exception) { /* ignored */ }
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

internal class RecurringJobRandomResult : BaseRecurringJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public RecurringJobRandomResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test random result", sleepDuration, true)
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

		return new JobFail(JobFailEnum.FailedExecute, this.ID, "test error");
	}
}

internal class RecurringJobOkResult : BaseRecurringJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public RecurringJobOkResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test success", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobOk(this.ID);
	}

}

internal class RecurringJobFailedExecuteResult : BaseRecurringJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public RecurringJobFailedExecuteResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name,"test error", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobFail(JobFailEnum.FailedExecute, this.ID, "test error");
	}

	
}

internal class RecurringJobInternalExceptionResult : BaseRecurringJob
{
	private TimeSpan JobSimulateWorkTime { get; }

	public RecurringJobInternalExceptionResult(string name, TimeSpan sleepDuration, TimeSpan jobSimulateWorkTime) : base(name, "test error", sleepDuration, true)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		throw new Exception("some internal exp");
	}

}

internal class TestRecurringScheduler : BaseRecurringScheduler
{
	public TestRecurringScheduler(Configuration? config) : base(config) { }

	public new bool Register(IRecurringJob job)
	{
		return base.Register(job);
	}

	public new int Register(IEnumerable<IRecurringJob> jobs)
	{
		return base.Register(jobs);
	}
}