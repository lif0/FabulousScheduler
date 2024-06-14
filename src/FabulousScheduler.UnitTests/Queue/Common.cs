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
	public QueueRandomJob(string name, string category, bool isAsyncAction, TimeSpan jobSimulateWorkTime) : base(name, category, isAsyncAction, null)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		int val = Random.Shared.Next(1, 10);
		await Task.Delay(JobSimulateWorkTime);
	    
		if (val % 2 == 0)
		{
			return new JobOk(this.ID, Name);
		}

		return new JobFail(QueueJobFailEnum.InternalException, this.ID, "QueueRandomJob", null);
	}
}

public class QueueJobFailResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobFailResult(string name, string category, bool isAsyncAction, TimeSpan jobSimulateWorkTime) : base(name, category, isAsyncAction, null)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobFail(QueueJobFailEnum.InternalException, this.ID, nameof(QueueJobFailResult));
	}
}

public class QueueJobOkResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobOkResult(string name, string category, bool isAsyncAction, TimeSpan jobSimulateWorkTime) : base(name, category, isAsyncAction, null)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		return new JobOk(this.ID, Name);
	}

}

public class QueueJobFailExceptionResult : BaseQueueJob
{
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobFailExceptionResult(string name, string category, bool isAsyncAction, TimeSpan jobSimulateWorkTime) : base(name, category, isAsyncAction, null)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		await Task.Delay(JobSimulateWorkTime);
		throw new Exception("some exp");
	}
}

public class QueueJobAttemptsFailNextOk : BaseQueueJob
{
	private long _countCall = 0;
	public TimeSpan JobSimulateWorkTime { get; }

	public QueueJobAttemptsFailNextOk(string name, string category, bool isAsyncAction, TimeSpan jobSimulateWorkTime) : base(name, category, isAsyncAction, 2)
	{
		JobSimulateWorkTime = jobSimulateWorkTime;
	}

	protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
	{
		Interlocked.Increment(ref _countCall);
		await Task.Delay(JobSimulateWorkTime);

		if (Interlocked.Read(ref _countCall) % 2 != 0)
		{
			throw new Exception("some exp");
		}
		else
		{
			return new JobOk(this.ID, this.Name);
		}
	}
}

public class TestQueueScheduler : BaseQueueScheduler
{
	public TestQueueScheduler(Configuration? config, IQueue queue) : base(config, queue)
	{
	}
}

public class TestQueueSchedulerWithAttempts : BaseQueueScheduler
{
	public TestQueueSchedulerWithAttempts(Configuration? config, IQueue queue) : base(config, queue)
	{
		base.JobResultEvent += OnJobResultEvent;
	}

	private void OnJobResultEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> e)
	{
		if (!e.IsFail || sender.Attempts.GetValueOrDefault(0) <= 0) return;
		sender.ResetState();
		base.Queue.Enqueue(sender);
	}
}