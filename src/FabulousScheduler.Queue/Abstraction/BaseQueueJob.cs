using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Queue.Interfaces;

namespace FabulousScheduler.Queue.Abstraction;

public abstract class BaseQueueJob : IQueueJob
{
	// sync
	private readonly object _lockState = new object();

	// private
	private bool _isAsyncAction;
	private QueueJobStateEnum _state;
	private bool _disposed;
	private uint _totalRun;


	// public
	public Guid ID { get; }
	public string Name { get; }
	public string Category { get; }
	public byte? Attempts { get; private set; }
	public DateTime? LastExecute { get; private set; }
	public DateTime? LastSuccessExecute { get; private set; }
	public uint TotalRun => _totalRun;

	protected BaseQueueJob(string name, string category, bool isAsyncAction, byte? attempts)
	{
		ID = Guid.NewGuid();
		Name = name;
		Category = category;
		Attempts = attempts;
		_isAsyncAction = isAsyncAction;
		_state = QueueJobStateEnum.Waiting;
	}

	#region Public method

	public QueueJobStateEnum State
	{
		get { lock (_lockState) { return _state; } }
	}

	public async Task<JobResult<JobOk, JobFail>> ExecuteAsync()
	{
		lock (_lockState)
		{
			if (_disposed)
			{
				return new JobFail(QueueJobFailEnum.Disposed, this.ID, $"The job {Name} was disposed");
			}
			
			if (_state is not QueueJobStateEnum.Waiting)
			{
				string msg = $"Incorrect state run the job. Job has state: {State}";
				return new JobFail(QueueJobFailEnum.IncorrectState, this.ID, msg);
			}

			_state = QueueJobStateEnum.Running;
			_totalRun++;
			if (Attempts.HasValue)
			{
				Attempts--;
			}
		}
		DateTime? dt = null;

		try
		{
			JobResult<JobOk, JobFail> res;
			
			if (_isAsyncAction)
			{
				res = await ActionJob();
			}
			else
			{
				res = ActionJob().Result; // because action is sync
			}
			
			dt = DateTime.Now;
			if (res.IsSuccess)
			{
				LastSuccessExecute = dt;
			}
			// else
			// {
			// 	Interlocked.Increment(ref _totalFail);
			// }

			return res;
		}
		catch (Exception e)
		{
			return new JobFail(QueueJobFailEnum.InternalException, this.ID, e.Message, e);
		}
		finally
		{
			_state = QueueJobStateEnum.Completed;
			LastExecute = dt ?? DateTime.Now;
		}
	}

	public void ResetState()
	{
		lock (_lockState)
		{
			if(_disposed || _state != QueueJobStateEnum.Completed) return;

			_state = QueueJobStateEnum.Waiting;
		}
	}

	public void Dispose() // TODO KGG :> потенциальное место для бага
	{
		_disposed = true;
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Abstract method

	protected abstract Task<JobResult<JobOk, JobFail>> ActionJob();

	#endregion
}