using Job.Core.Types;
using JobScheduler.Queue.Enums;
using JobScheduler.Queue.Interfaces;
using JobScheduler.Queue.Result;

namespace JobScheduler.Queue.Abstraction;

public class BaseQueueJob : IQueueJob
{
	#region Private

	private readonly object _lock = new object();

	private bool _disposed;
	private ushort _totalRun;

	#endregion

	#region Info

	public Guid Id { get; }
	public string Name { get; }
	public JobState State { get; private set; }

	#endregion

	#region Data
	
	public DateTime? LastExecute { get; private set; }
	public DateTime? LastSuccessExecute { get; private set; }
	
	public ushort TotalRun => _totalRun;

	#endregion

	#region Protected

	protected Func<Task<JobResult<JobOk, JobFail>>>? ActionJob;

	#endregion

	protected BaseQueueJob(string name)
	{
		Id = Guid.NewGuid();
		Name = name;
	}

	#region Public methods
	
	public async Task<JobResult<JobOk, JobFail>> ExecuteAsync()
	{
		if (_disposed)
		{
			return new JobFail(Id, Name, ReasonFail.Disposed, $"The job {Name} was disposed", null);
		}

		if (ActionJob == null)
		{
			return new JobFail(Id, Name, ReasonFail.InternalException, $"ActionJob not initialized.", null);
		}
		
		lock (_lock)
		{
			if (State is JobState.Running)
			{
				string msg = $"Incorrect state run the job. Job {Name} current state: {State}";
				return new JobFail(Id, Name, ReasonFail.IncorrectState, msg, null);
			}

			State = JobState.Running;
			_totalRun++;
		}

		var dt = DateTime.Now;

		try
		{
			var res = await ActionJob?.Invoke()!;

			if (res.IsSuccess)
			{
				LastSuccessExecute = dt;
			}

			return res;
		}
		catch (Exception e)
		{
			return new JobFail(Id, Name, ReasonFail.InternalException, e.Message, e);
		}
		finally
		{
			State = QueueJobStateEnum.Completed;
			LastExecute = dt;
		}
	}

	public void SetWaiting()
	{
		if(_disposed) return;

		lock (_lock)
		{
			State = JobState.Waiting;
		}
	}

	#endregion

	public void Dispose()
	{
		_disposed = true;
		ActionJob = null;
	}

	public ValueTask DisposeAsync()
	{
		_disposed = true;
		ActionJob = null;
		return ValueTask.CompletedTask;
	}
}