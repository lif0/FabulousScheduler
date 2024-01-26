using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FabulousScheduler.Cron.Interfaces;

namespace FabulousScheduler.Cron.Abstraction;

public abstract class BaseCronJobScheduler : ICronJobScheduler
{
	// private
	private readonly SemaphoreSlim _jobParallelPool;
	private readonly ConcurrentDictionary<Guid, (ICronJob, Task)> _inProgress;
	private readonly ConcurrentQueue<ICronJob> _queue;

	// protected
	protected readonly Config Config;
	protected readonly ConcurrentDictionary<Guid, ICronJob> Jobs;

	// public
	public event ICronJobScheduler.JobResultEventHandler? JobResultEvent;

	protected BaseCronJobScheduler(Config? config)
	{
		Config = config ?? Config.Default;
		Jobs = new ConcurrentDictionary<Guid, ICronJob>();

		_jobParallelPool = new SemaphoreSlim(Config.MaxParallelJobExecute, Config.MaxParallelJobExecute);
		_inProgress = new ConcurrentDictionary<Guid, (ICronJob, Task)>(Environment.ProcessorCount, this.Config.MaxParallelJobExecute);
		_queue = new ConcurrentQueue<ICronJob>();
		
		Task.Factory.StartNew(ExecutableLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}
	
	/// <summary>
	/// Register job
	/// </summary>
	/// <param name="job">job instance</param>
	/// <returns>true - if job is registered, otherwise false</returns>
	protected bool Register(ICronJob job)
	{
		return Jobs.TryAdd(job.ID, job);
	}

	/// <summary>
	/// Register a jobs
	/// </summary>
	/// <param name="jobs">jobs</param>
	/// <returns>count success registered jobs</returns>
	protected int Register(IEnumerable<ICronJob> jobs)
	{
		int fail = 0;
		foreach (var job in jobs)
		{
			if (!this.Jobs.TryAdd(job.ID, job))
			{
				fail++;
			}
		}

		return fail;
	}

	#region Public
	public int CurrentRunnableJobCount() => _inProgress.Count;
	#endregion
	
	#region Private

	/// <returns>Time: O(n) - BAD</returns>
	private bool TryScheduleJobs()
	{
		var jobs = Jobs
			.Where(x => x.Value.State == State.Ready)
			.Select(x => x.Value)
			.OrderBy(x => x.LastExecute)
			.ToArray();

		if (jobs.Length == 0) return false;
		
		foreach (var job in jobs)
		{
			job.SetStateWaiting();
			_queue.Enqueue(job);
		}

		return true;
	} 

	private async void ExecutableLoop()
	{
		while (true)
		{
			if (this._queue.IsEmpty)
			{
				if (!TryScheduleJobs())
				{
					await Task.Delay(this.Config.SleepAfterCheck, CancellationToken.None);
					continue;
				}
			}

			await _jobParallelPool.WaitAsync(CancellationToken.None);

			if (_queue.TryDequeue(out ICronJob? job))
			{
				CreateTask(ref job);
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CreateTask(ref ICronJob job)
	{
		var task = Task.Factory.StartNew(async obj =>
		{
			var @job = obj as ICronJob;
			if (@job is null)
			{
				ArgumentNullException.ThrowIfNull(@job, nameof(@job));
			}

			var res = await @job.ExecuteAsync();
			if (_inProgress.TryRemove(@job.ID, out var tup))
			{
				JobResultEvent?.Invoke(ref tup.Item1, ref res);
			}
			_jobParallelPool.Release(1);
		}, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

		_inProgress.TryAdd(job.ID, (job, task));
	}

	#endregion
}