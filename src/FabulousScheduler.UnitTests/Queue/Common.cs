using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Result;

namespace Job.Core.Tests.Queue;

public class QueueRandomJob : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueRandomJob(string name, TimeSpan jobSimulateWorkTime) : base(name)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		int val = Random.Shared.Next(1, 10);
		await Task.Delay(JobSimulateWorkTime);

		if (val % 2 == 0)
		{
			return new JobOk(Id, Name);
		}

		return new JobFail(Id, Name,QueueJobFailEnum.InternalException, "lol", null);
	}
}

public class QueueJobFailResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobFailResult(string name, TimeSpan jobSimulateWorkTime) : base(name)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobFail(Id, Name, QueueJobFailEnum.InternalException, "lol", null);
	}

	
}

public class QueueJobOkResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobOkResult(string name, TimeSpan jobSimulateWorkTime) : base(name)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobOk(Id, Name);
	}

	
}

public class QueueJobFailExceptionResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobFailExceptionResult(string name, TimeSpan jobSimulateWorkTime) : base(name)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(JobSimulateWorkTime);
		throw new Exception("some exp");
	}

}

public class TestQueueJobManager : BaseQueueJobManager
{
	public TestQueueJobManager(IQueue queue, Config? config = null) : base(queue, config)
	{
		
	}

	public void Start()
	{
		base.StartProcessing();
	}
	
	public void Stop()
	{
		base.StopProcessing();
	}
	
	public Task FinishAllAndStopAsync()
	{
		return base.WaitFinishAllJobAndStopAsync();
	}

}

public class TestQueueReAddFailJobManager : BaseQueueJobManager
{

	public int AttepmentAfterFail { get; }
	public TestQueueReAddFailJobManager(IQueue queue, Config? config = null, int attepmentAfterFail=1) : base(queue, config)
	{
		AttepmentAfterFail = attepmentAfterFail;
		base.JobResultEvent += OnJobResultEvent;
	}

	private void OnJobResultEvent(IQueueJob job, JobResult<JobOk, JobFail> e)
	{
		if (e.IsFail && job.TotalRun < AttepmentAfterFail)
		{
			job.SetWaiting();
			this.Queue.Enqueue(job);
		}
	}


	public void Start()
	{
		base.StartProcessing();
	}
	
	public void Stop()
	{
		base.StopProcessing();
	}
	public Task FinishAllAndStopAsync()
	{
		return base.WaitFinishAllJobAndStopAsync();
	}

}