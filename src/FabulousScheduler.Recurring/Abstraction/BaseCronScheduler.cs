using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Core.Interfaces;
using System.Collections.Concurrent;

namespace FabulousScheduler.Recurring.Abstraction;

public abstract class BaseRecurringScheduler : IRecurringJobScheduler
{
	// sync
	private readonly object _jobsDictLocker = new();
	private readonly object _mainLoopLocker = new();

	// private
	private Task? _producerLoop;
	private Task[]? _workers;
	private int _inProgressCount;
	private readonly SemaphoreSlim _queueSignal;
	private readonly ConcurrentQueue<IRecurringJob> _queue;
	private readonly CancellationTokenSource _cancellationTokenSource;
	private readonly Dictionary<Guid, IRecurringJob> _registeredJob;

	// protected
	protected readonly Configuration Config;

	// public
	public event IRecurringJobScheduler.JobResultEventHandler? JobResultEvent;

	protected BaseRecurringScheduler(Configuration? config)
	{
		Config = config ?? Configuration.Default;

		_registeredJob = new Dictionary<Guid, IRecurringJob>();
		_queueSignal = new SemaphoreSlim(0);
		_queue = new ConcurrentQueue<IRecurringJob>();
		_cancellationTokenSource = new CancellationTokenSource();
	}

	/// <summary>
	/// Register job
	/// </summary>
	/// <param name="job">job instance</param>
	/// <returns>true - if job is registered, otherwise false</returns>
	protected bool Register(IRecurringJob job)
	{
		lock (_jobsDictLocker)
		{
			return RegisterUnsafe(job);
		}
	}

	/// <summary>
	/// Register a jobs
	/// </summary>
	/// <param name="jobs">jobs</param>
	/// <returns>count success registered jobs</returns>
	protected int Register(IEnumerable<IRecurringJob> jobs)
	{
		int success = 0;
		lock (_jobsDictLocker)
		{
			foreach (IRecurringJob job in jobs)
			{
				if (RegisterUnsafe(job))
				{
					success++;
				}
			}
		}

		return success;
	}

	#region Public
	public int CurrentRunnableJobCount() => Volatile.Read(ref _inProgressCount);

	/// <inheritdoc cref="IJobScheduler.RunScheduler"/>
	public void RunScheduler()
	{
		lock (_mainLoopLocker)
		{
			if(_producerLoop != null) return;

			// Consumers: a fixed pool of workers drains the queue (bounds parallelism).
			int workerCount = Config.MaxParallelJobExecute;
			var workers = new Task[workerCount];
			for (int i = 0; i < workerCount; i++)
			{
				workers[i] = Task.Run(WorkerLoop);
			}
			_workers = workers;

			// Producer: scans registered jobs and feeds ready ones to the queue.
			_producerLoop = Task.Factory.StartNew(ScheduleLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}
	}

	#endregion

	#region Private

	/// <summary>
	/// Producer loop. Runs on a dedicated long-running thread: scans for ready jobs and
	/// enqueues them, backing off by <see cref="Configuration.SleepAfterCheck"/> when there
	/// is nothing to schedule.
	/// </summary>
	private void ScheduleLoop()
	{
		var token = _cancellationTokenSource.Token;
		while (!token.IsCancellationRequested)
		{
			if (!TryScheduleJobs())
			{
				// this is executed on a dedicated thread
				Thread.Sleep(this.Config.SleepAfterCheck);
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}

	/// <returns>Time: O(n) - BAD</returns>
	private bool TryScheduleJobs()
	{
		IRecurringJob[] readyJobs;
		lock (_jobsDictLocker)
		{
			readyJobs = _registeredJob
				.Where(x => x.Value.State == State.Ready)
				.Select(x => x.Value)
				.OrderBy(x => x.LastExecute)
				.ToArray();
		}

		if (readyJobs.Length == 0) return false;

		foreach (var job in readyJobs)
		{
			job.ResetState();
			_queue.Enqueue(job);
			_queueSignal.Release();
		}

		return true;
	}

	/// <summary>
	/// Consumer loop. Each worker waits for a queued job, executes it and raises the result
	/// event. There are <see cref="Configuration.MaxParallelJobExecute"/> workers, so the
	/// parallelism is bounded without a per-job Task dispatch.
	/// </summary>
	private async Task WorkerLoop()
	{
		var token = _cancellationTokenSource.Token;
		try
		{
			while (!token.IsCancellationRequested)
			{
				await _queueSignal.WaitAsync(token).ConfigureAwait(false);

				if (!_queue.TryDequeue(out IRecurringJob? dequeued) || dequeued is null)
				{
					continue;
				}

				IRecurringJob job = dequeued;
				Interlocked.Increment(ref _inProgressCount);
				try
				{
					var res = await job.ExecuteAsync().ConfigureAwait(false);
					JobResultEvent?.Invoke(ref job, ref res);
				}
				finally
				{
					Interlocked.Decrement(ref _inProgressCount);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// scheduler is shutting down
		}
	}

	private bool RegisterUnsafe(IRecurringJob job)
	{
		return _registeredJob.TryAdd(job.ID, job);
	}

	#endregion

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		_producerLoop?.Dispose();
		_queueSignal.Dispose();
		_workers = null;
	}
}
