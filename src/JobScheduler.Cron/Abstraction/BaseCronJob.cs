using Job.Core.Types;
using JobScheduler.Cron.Interfaces;
using JobScheduler.Cron.Enums;
using JobScheduler.Cron.Result;

namespace JobScheduler.Cron.Abstraction;

public abstract class BaseCronJob : ICronJob
{
	#region Private
	
	private readonly object _lock = new object();

	private bool _disposed;
	private Task? _taskWakeJob;
	private long _totalFail;
	private long _totalRun;
	private CronJobStateEnum _state;

	#endregion

	#region Info

	public string Name { get; }
	public string Category { get; }

	public State State
	{
		get
		{
			TryWakeUpJob();
			return _state;
		}
	}

	public TimeSpan SleepDuration { get; }

	#endregion

	#region Data
	
	public DateTime? LastExecute { get; private set; }
	public DateTime? LastSuccessExecute { get; private set; }
	
	public long TotalRun => _totalRun;
	public long TotalFail => _totalFail;

	#endregion
	
	protected BaseCronJob(string uniqName,  string category, TimeSpan sleepDuration)
	{
		Name = uniqName;
		Category = category;
		this._state = CronJobStateEnum.Ready;

		if (sleepDuration != TimeSpan.MaxValue && sleepDuration != TimeSpan.MinValue && sleepDuration != TimeSpan.Zero)
		{
			SleepDuration = sleepDuration;
		}
		else
		{
			SleepDuration = TimeSpan.Zero;
		}
	}

	#region Public methods

	public async Task<JobResult<JobOk, JobFail>> ExecuteAsync()
	{
		if (_disposed)
		{
			return new JobFail(CronJobFailEnum.Disposed, $"The job {Name} was disposed", null);
		}

		lock (_lock)
		{
			if (_state is not (CronJobStateEnum.Ready or CronJobStateEnum.Waiting))
			{
				string msg = $"Incorrect state run the job. Job {Name} current state: {State}";
				return new JobFail(CronJobFailEnum.IncorrectState, msg, null);
			}

			_state = CronJobStateEnum.Running;
		}
		Interlocked.Increment(ref _totalRun);

		DateTime? dt = null;

		try
		{
			var res = await ActionJob();
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
			return new JobFail(CronJobFailEnum.InternalException, e.Message, e);
		}
		finally
		{
			//SleepAndWakeAfterPeriod(LastSuccessExecute == dt);
			_state = CronJobStateEnum.Sleeping;
			LastExecute = dt ?? DateTime.Now;
		}
	}
	
	public void SetWaiting()
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
		if (_taskWakeJob != null)
		{
			_taskWakeJob.GetAwaiter().GetResult();
		}
	}

	public async ValueTask DisposeAsync()
	{
		_disposed = true;
		if (_taskWakeJob != null)
		{
			await _taskWakeJob;
		}
	}

	#endregion

	#region Private methods

	private void SleepAndWakeAfterPeriod(bool jobSuccessFinish)
	{
		if (jobSuccessFinish && SleepDuration != TimeSpan.Zero)
		{
			if (_taskWakeJob is { IsCompleted: false })
			{
				return; //skip if not completed
			}
	
			lock (_lock)
			{
				_state = CronJobStateEnum.Sleeping;
			}
			
			_taskWakeJob = Task.Delay(SleepDuration).ContinueWith(_ =>
			{
				lock (_lock)
				{
					_state = CronJobStateEnum.Ready;
				}
			}, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
		}
		else
		{
			lock (_lock)
			{
				_state = CronJobStateEnum.Ready;
			}
		}
	}
	
	private void TryWakeUpJob()
	{
		if(_state is not CronJobStateEnum.Sleeping || SleepDuration == TimeSpan.Zero) return;

		lock (_lock)
		{
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