using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Core.Interfaces;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace FabulousScheduler.Recurring.Abstraction;

public abstract class BaseRecurringScheduler : IRecurringJobScheduler
{
#if DEBUGWITHCONSOLE
	private readonly Guid _debugId = Guid.NewGuid();
#endif
	
	// sync
	private readonly object _jobsDictLocker = new();
	private readonly object _mainLoopLocker = new();
	
	// private
	private Task? _mainLoop;
	private readonly SemaphoreSlim _jobExecutorLimiter;
	private readonly ConcurrentDictionary<Guid, (IRecurringJob, Task)> _inProgress;
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
		_jobExecutorLimiter = new SemaphoreSlim(Config.MaxParallelJobExecute, Config.MaxParallelJobExecute);
		_inProgress = new ConcurrentDictionary<Guid, (IRecurringJob, Task)>(Environment.ProcessorCount, this.Config.MaxParallelJobExecute);
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
	public int CurrentRunnableJobCount() => _inProgress.Count;

	/// <inheritdoc cref="IJobScheduler.RunScheduler"/>
	public void RunScheduler()
	{
		lock (_mainLoopLocker)
		{
			if(_mainLoop != null) return;

			_mainLoop = Task.Factory.StartNew(ExecutableLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}
	}
	
	#endregion
	
	#region Private

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

#if DEBUGWITHCONSOLE
		Console.WriteLine($"[TryScheduleJobs {_debugId:N}] FoundJobs:{readyJobs.Length} AllJobs:{_registeredJob.Count}");
#endif

		if (readyJobs.Length == 0) return false;
		
		foreach (var job in readyJobs)
		{
			job.ResetState();
			_queue.Enqueue(job);
		}

		return true;
	} 

	private async void ExecutableLoop()
	{
		while (!_cancellationTokenSource.Token.IsCancellationRequested)
		{
			if (this._queue.IsEmpty)
			{
				if (!TryScheduleJobs())
				{
#if DEBUGWITHCONSOLE
					Console.WriteLine($"[ExecutableLoop {_debugId:N}] Start sleep on {this.Config.SleepAfterCheck.TotalMinutes}");
#endif
					// this is executed on a dedicated thread
					Thread.Sleep(this.Config.SleepAfterCheck);
					continue;
				}
			}
#if DEBUGWITHCONSOLE
			Console.WriteLine($"[ExecutableLoop {_debugId:N}] Start wait JobParallelLimiter");
#endif
			await _jobExecutorLimiter.WaitAsync(CancellationToken.None);
#if DEBUGWITHCONSOLE
			Console.WriteLine($"[ExecutableLoop {_debugId:N}] Finish wait JobParallelLimiter");
#endif

			if (_queue.TryDequeue(out IRecurringJob? job))
			{
				CreateTask(ref job);
#if DEBUGWITHCONSOLE
				Console.WriteLine($"[_queue.TryDequeue {_debugId:N}] Dequeued a job");
#endif
			}
		}
		// ReSharper disable once FunctionNeverReturns
	}
	
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void CreateTask(ref IRecurringJob job)
	{
		var task = Task.Factory.StartNew(async obj =>
		{
			var @job = obj as IRecurringJob;
			if (@job is null)
			{
				ArgumentNullException.ThrowIfNull(@job, nameof(@job));
			}

#if DEBUGWITHCONSOLE
			Console.WriteLine($"[CreateTask {_debugId:N}] JobResultEventNull:{JobResultEvent is null}");
#endif
			var res = await @job.ExecuteAsync();
			if (_inProgress.TryRemove(@job.ID, out var tup))
			{
				JobResultEvent?.Invoke(ref tup.Item1, ref res);
			}
			_jobExecutorLimiter.Release(1);
		}, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

		_inProgress.TryAdd(job.ID, (job, task));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool RegisterUnsafe(IRecurringJob job)
	{
#if DEBUGWITHCONSOLE
		bool result = _registeredJob.TryAdd(job.ID, job);
		Console.WriteLine($"[RegisterUnsafe {_debugId:N}] jobID:{job.ID} AllJobs:{_registeredJob.Count}");
		return result;
#else
		return _registeredJob.TryAdd(job.ID, job);
#endif
	}
	
	#endregion

	public void Dispose()
	{
		_cancellationTokenSource.Cancel();
		_cancellationTokenSource.Dispose();
		_mainLoop?.Dispose();
		_jobExecutorLimiter.Dispose();
	}
}