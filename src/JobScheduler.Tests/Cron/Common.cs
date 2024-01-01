using Job.Core.Types;
using JobScheduler.Cron.Abstraction;
using JobScheduler.Cron.Enums;
using JobScheduler.Cron.Interfaces;
using JobScheduler.Cron.Result;

namespace Job.Core.Tests.Cron;

public class CronJobRandomResult : BaseCronJob
{
	public TimeSpan timeWorkJob { get; }

	public CronJobRandomResult(string uniqName, TimeSpan sleepDuration, TimeSpan time) : base(uniqName,"test", sleepDuration)
	{
		timeWorkJob = time;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		int val = Random.Shared.Next(1, 10);
		await Task.Delay(timeWorkJob);

		if (val % 2 == 0)
		{
			return new JobOk();
		}

		return new JobFail(CronJobFailEnum.InternalException, "lol", null);
	}
}

public class CronJobFailResult : BaseCronJob
{
	public TimeSpan timeWorkJob { get; }

	public CronJobFailResult(string uniqName, TimeSpan sleepDuration, TimeSpan time) : base(uniqName,"test", sleepDuration)
	{
		timeWorkJob = time;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(timeWorkJob);
		return new JobFail(CronJobFailEnum.InternalException, "lol", null);
	}

	
}

public class CronJobOkResult : BaseCronJob
{
	public TimeSpan timeWorkJob { get; }

	public CronJobOkResult(string uniqName, TimeSpan sleepDuration, TimeSpan time) : base(uniqName,"test", sleepDuration)
	{
		timeWorkJob = time;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(timeWorkJob);
		return new JobOk();
	}

	
}

public class CronJobFailExceptionResult : BaseCronJob
{
	public TimeSpan timeWorkJob { get; }

	public CronJobFailExceptionResult(string uniqName, TimeSpan sleepDuration, TimeSpan time) : base(uniqName, "test", sleepDuration)
	{
		timeWorkJob = time;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(timeWorkJob);
		throw new Exception("some exp");
	}

}

public class CronJobManagerManualRecheck : BaseCronJobManager
{
	public TimeSpan TakeJobsIdleMore { get; set; }
	public CronJobManagerManualRecheck(int maxParallelJob, TimeSpan? idleMode = null) : base(maxParallelJob)
	{
		if (idleMode.HasValue)
		{
			TakeJobsIdleMore = idleMode.Value;
		}
		else
		{
			TakeJobsIdleMore = TimeSpan.FromSeconds(10);
		}
	}
	
	public async Task RecheckJobs()
	{
		var result = await ExecuteReadyJob();
		
		Console.WriteLine();
	}
	
	public bool Register(ICronJob job)
	{
		return base.Register(job);
	}

	public int Register(IEnumerable<ICronJob> jobs)
	{
		return base.Register(jobs);
	}

}