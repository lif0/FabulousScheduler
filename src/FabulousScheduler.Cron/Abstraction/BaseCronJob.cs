using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Abstraction;

public abstract class BaseCronJob : ICronJob
{
	#region Private
	
	private readonly object _lock = new object();

	private bool _disposed;
	private long _totalFail;
	private long _totalRun;
	private CronJobStateEnum _state;

	#endregion

	#region Protected

	protected bool IsAsyncAction { get; }

	#endregion
	
	#region Info

	public string Name { get; }
	public string Category { get; }
	public TimeSpan SleepDuration { get; }

	public CronJobStateEnum State
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
	
	public long TotalRun => _totalRun;
	public long TotalFail => _totalFail;

	#endregion
	
	protected BaseCronJob(string name,  string category, TimeSpan sleepDuration, bool isAsyncAction)
	{
		ID = Guid.NewGuid();
		Name = name;
		Category = category;
		IsAsyncAction = isAsyncAction;
		this._state = CronJobStateEnum.Ready;

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
				_state = CronJobStateEnum.Disposed;
				return new JobFail(this.ID, CronJobFailEnum.Disposed, $"The job {Name} was disposed", null);
			}
			
			if (_state is not (CronJobStateEnum.Ready or CronJobStateEnum.Waiting))
			{
				string msg = $"Incorrect state run the job. Job {Name} current state: {State}";
				return new JobFail(this.ID, CronJobFailEnum.IncorrectState, msg, null);
			}

			_state = CronJobStateEnum.Running;
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
				res = ActionJob().Result; // because a action is sync
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
			return new JobFail(this.ID,CronJobFailEnum.InternalException, e.Message, e);
		}
		finally
		{
			_state = CronJobStateEnum.Sleeping;
			LastExecute = dt ?? DateTime.Now;
		}
	}

	void ICronJob.SetStateWaiting()
	{
		lock (_lock)
		{
			if(_disposed || _state != CronJobStateEnum.Ready) return;

			_state = CronJobStateEnum.Waiting;
		}
	}


	public void Dispose()
	{
		_disposed = true;
		if (_state is CronJobStateEnum.Ready)
		{
			_state = CronJobStateEnum.Disposed;
		}
	}

	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	#endregion

	#region Private methods

	private void UpdateState()
	{
		if(_state is not CronJobStateEnum.Sleeping) return;
		if(SleepDuration == TimeSpan.MaxValue) return;

		lock (_lock)
		{
			if (SleepDuration == TimeSpan.Zero)
			{
				_state = CronJobStateEnum.Ready;
			}

			if (LastSuccessExecute == null || DateTime.Now.Subtract(LastSuccessExecute.Value.AddMinutes(SleepDuration.TotalMinutes)).Ticks > 0)
			{
				_state = CronJobStateEnum.Ready;
			}
		}
	}
	
	#endregion
	
	#region Abstract method

	protected abstract Task<JobResult<JobOk, JobFail>> ActionJob();

	#endregion
}