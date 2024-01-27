using System.Collections.Concurrent;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Result;

namespace FabulousScheduler.Queue.Abstraction;

public abstract class BaseQueueJobScheduler : IQueueJobScheduler
{
	#region Default

	private const int DefaultMaxParallelJobExecute = 25;
	private readonly TimeSpan DefaultSleepIfQueueIsEmpty = new TimeSpan(0, 0, 10);

	#endregion
	
	#region Private field
	
	private Task? _loop;
	private readonly CancellationTokenSource _cancellationToken;
	
	
	private readonly object _lock;
	private readonly SemaphoreSlim _jobParallelPool;
	private readonly Config _config;

	#endregion

	#region Flags

	private bool _stopped;
	private bool _started;

	#endregion

	#region Protected field

	protected readonly IQueue Queue;
	protected readonly ConcurrentDictionary<Guid, (IQueueJob, Task)> JobInProgress;
	protected delegate void JobResultHandler(IQueueJob job, JobResult<JobOk, JobFail> e);
	protected event JobResultHandler? JobResultEvent;

	#endregion

	public BaseQueueJobScheduler(IQueue queue, Config? config = null)
	{
		_cancellationToken = new CancellationTokenSource();

		_config = config ?? new Config(DefaultMaxParallelJobExecute, DefaultSleepIfQueueIsEmpty);
		_jobParallelPool = new SemaphoreSlim(_config.MaxParallelJobExecute, _config.MaxParallelJobExecute);
		Queue = queue;
		JobInProgress = new ConcurrentDictionary<Guid, (IQueueJob, Task)>(Environment.ProcessorCount, _config.MaxParallelJobExecute);
		

		_lock = new object();
		_stopped = false;
		_started = false;
	}

	#region Protected

	protected void StartProcessing()
	{
		lock (_lock)
		{
			if(_started) return;
			_started = true;
		}
		
		_loop = Task.Factory.StartNew(async () =>
		{
			while (!_cancellationToken.Token.IsCancellationRequested)
			{
				if (Queue.Count == 0)
				{
					await Task.Delay(_config.SleepIfQueueIsEmpty, _cancellationToken.Token);
					continue;
				}

				await _jobParallelPool.WaitAsync(_cancellationToken.Token);

				lock (_lock)
				{
					var job = Queue.TryDequeue();
					if (job == null) continue;

					RunJob(ref job);
				}
			}
		}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
	}

	protected bool StopProcessing()
	{
		lock (_lock)
		{
			if(_stopped) return false;
			_stopped = true;
		}
		
		_cancellationToken.Cancel();
		_cancellationToken.Dispose();

		return true;
	}
	
	protected async Task<bool> WaitFinishAllJobAndStopAsync()
	{
		if (_loop == null)
		{
			throw new NullReferenceException("Queue processor is not started");
		}

		if (!StopProcessing())
		{
			return true;
		}

		if (Queue.Count > 0)
		{
			while (Queue.Count != 0)
			{
				await _jobParallelPool.WaitAsync();
				var job = Queue.TryDequeue();
				if(job == null) continue;
					
				RunJob(ref job);
			}
		}
			
		if (JobInProgress.Count > 0)
		{
			var tasks = JobInProgress.Select(x => x.Value.Item2).ToArray();
			Task.WaitAll(tasks);
		}

		return true;
	}

	#endregion

	#region Private

	private void RunJob(ref IQueueJob job)
	{
		var task = Task.Factory.StartNew(async obj =>
		{
			var @job = obj as IQueueJob;
			if (@job is null)
			{
				ArgumentNullException.ThrowIfNull(@job);
			}

			var res = await @job.ExecuteAsync();
			if (JobInProgress.TryRemove(@job.ID, out var tup))
			{
				JobResultEvent?.Invoke(tup.Item1, res);
			}
			_jobParallelPool.Release();
		}, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

		JobInProgress.TryAdd(job.ID, (job, task));
	}

	#endregion

	public void Dispose()
	{
		_loop?.Dispose();
		_cancellationToken.Dispose();
		_jobParallelPool.Dispose();
	}

	public void RunScheduler()
	{
		throw new NotImplementedException();
	}
}