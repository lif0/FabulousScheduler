using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Result;

namespace Job.Core.Tests.Queue;

public class QueueRandomJob : BaseQueueJob
{
	public TimeSpan timeWorkJob { get; }

	public QueueRandomJob(string uniqName, TimeSpan time) : base(uniqName)
	{
		timeWorkJob = time;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		int val = Random.Shared.Next(1, 10);
		await Task.Delay(timeWorkJob);

		if (val % 2 == 0)
		{
			return new JobOk(Id, Name);
		}

		return new JobFail(Id, Name,QueueJobFailEnum.InternalException, "lol", null);
	}
}

public class QueueJobFailResult : BaseQueueJob
{
	public TimeSpan timeWorkJob { get; }

	public QueueJobFailResult(string uniqName, TimeSpan time) : base(uniqName)
	{
		timeWorkJob = time;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(timeWorkJob);
		return new JobFail(Id, Name, QueueJobFailEnum.InternalException, "lol", null);
	}

	
}

public class QueueJobOkResult : BaseQueueJob
{
	public TimeSpan timeWorkJob { get; }

	public QueueJobOkResult(string uniqName, TimeSpan time) : base(uniqName)
	{
		timeWorkJob = time;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(timeWorkJob);
		return new JobOk(Id, Name);
	}

	
}

public class QueueJobFailExceptionResult : BaseQueueJob
{
	public TimeSpan timeWorkJob { get; }

	public QueueJobFailExceptionResult(string uniqName, TimeSpan time) : base(uniqName)
	{
		timeWorkJob = time;
		base.ActionJob = SomeLogic;
	}
	
	private async Task<JobResult<JobOk, JobFail>> SomeLogic()
	{
		await Task.Delay(timeWorkJob);
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