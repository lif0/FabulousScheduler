using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Recurring.Result;
using FabulousScheduler.Recurring.Enums;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Recurring.Abstraction;

/// <summary> Base recurring job </summary>
public abstract class BaseRecurringJob : IRecurringJob
{
	#region Private
	
	private readonly object _lock = new object();

	private bool _disposed;
	private ulong _totalFail;
	private ulong _totalRun;
	private JobStateEnum _state;

	#endregion

	#region Protected

	protected bool IsAsyncAction { get; }

	#endregion
	
	#region Info

	public string Name { get; }
	public string Category { get; }
	public TimeSpan SleepDuration { get; }

	public JobStateEnum State
	{
		get
		{
			UpdateState();
			return _state;
		}
	}

	#endregion

	#region Data

	public Guid ID { get; }
	public DateTime? LastExecute { get; private set; }
	public DateTime? LastSuccessExecute { get; private set; }
	
	public ulong TotalRun => _totalRun;
	public ulong TotalFail => _totalFail;

	#endregion
	
	protected BaseRecurringJob(string name,  string category, TimeSpan sleepDuration, bool isAsyncAction)
	{
		ID = Guid.NewGuid();
		Name = name;
		Category = category;
		IsAsyncAction = isAsyncAction;
		this._state = JobStateEnum.Ready;

		if (sleepDuration  == TimeSpan.MinValue || sleepDuration == TimeSpan.Zero)
		{
			SleepDuration = TimeSpan.Zero;
		}
		else
		{
			SleepDuration = sleepDuration;
		}
	}

	#region Public methods

	public async Task<JobResult<JobOk, JobFail>> ExecuteAsync()
	{
		lock (_lock)
		{
			if (_disposed)
			{
				_state = JobStateEnum.Disposed;
				return new JobFail(JobFailEnum.Disposed, this.ID, $"The job {Name} was disposed");
			}
			
			if (_state is not (JobStateEnum.Ready or JobStateEnum.Waiting))
			{
				string msg = $"Incorrect state run the job. Job has state: {State}";
				return new JobFail(JobFailEnum.IncorrectState, this.ID, msg);
			}

			_state = JobStateEnum.Running;
		}
		Interlocked.Increment(ref _totalRun);

		DateTime? dt = null;

		try
		{
			JobResult<JobOk, JobFail> res;
			
			if (IsAsyncAction)
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
			else
			{
				Interlocked.Increment(ref _totalFail);
			}

			return res;
		}
		catch (Exception e)
		{
			Interlocked.Increment(ref _totalFail);
			return new JobFail(JobFailEnum.InternalException, this.ID, e.Message, e);
		}
		finally
		{
			_state = JobStateEnum.Sleeping;
			LastExecute = dt ?? DateTime.Now;
		}
	}

	void IRecurringJob.ResetState()
	{
		lock (_lock)
		{
			if(_disposed || _state != JobStateEnum.Ready) return;

			_state = JobStateEnum.Waiting;
		}
	}


	public void Dispose()
	{
		_disposed = true;
		if (_state is JobStateEnum.Ready)
		{
			_state = JobStateEnum.Disposed;
		}
	}

	public ValueTask DisposeAsync() // TODO KGG :> выглядит тупо, убери если нужно
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	private void UpdateState()
	{
		if(_state is not JobStateEnum.Sleeping) return;
		if(SleepDuration == TimeSpan.MaxValue) return;

		lock (_lock)
		{
			if (SleepDuration == TimeSpan.Zero)
			{
				_state = JobStateEnum.Ready;
			}

			if (LastSuccessExecute == null || DateTime.Now.Subtract(LastSuccessExecute.Value.AddMinutes(SleepDuration.TotalMinutes)).Ticks > 0)
			{
				_state = JobStateEnum.Ready;
			}
		}
	}
	
	#endregion
	
	#region Abstract method

	protected abstract Task<JobResult<JobOk, JobFail>> ActionJob();

	#endregion
}