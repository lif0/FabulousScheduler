using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Result;

namespace FabulousScheduler.Queue.Abstraction;

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
	public QueueJobStateEnum State { get; private set; }

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
			return new JobFail(Id, Name, QueueJobFailEnum.Disposed, $"The job {Name} was disposed", null);
		}

		if (ActionJob == null)
		{
			return new JobFail(Id, Name, QueueJobFailEnum.InternalException, $"ActionJob not initialized.", null);
		}
		
		lock (_lock)
		{
			if (State is QueueJobStateEnum.Running)
			{
				string msg = $"Incorrect state run the job. Job {Name} current state: {State}";
				return new JobFail(Id, Name, QueueJobFailEnum.IncorrectState, msg, null);
			}

			State = QueueJobStateEnum.Running;
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
			return new JobFail(Id, Name, QueueJobFailEnum.InternalException, e.Message, e);
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
			State = QueueJobStateEnum.Waiting;
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