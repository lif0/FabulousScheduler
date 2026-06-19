using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Core.Interfaces;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FabulousScheduler.Recurring.Abstraction;

public abstract class BaseRecurringScheduler : IRecurringJobScheduler
{
	// sync
	private readonly object _jobsDictLocker = new();
	private readonly object _mainLoopLocker = new();

	// scheduling: a producer keeps a min-heap of jobs keyed by their next-run time, so picking
	// the next job to run is O(log n) instead of an O(n) scan over every registered job.
	private static readonly TimeSpan MaxWait = TimeSpan.FromHours(1);
	private Task? _producerLoop;
	private Task[]? _workers;
	private int _inProgressCount;
	private readonly PriorityQueue<IRecurringJob, DateTime> _schedule;              // producer-owned
	private readonly ConcurrentQueue<(IRecurringJob job, DateTime when)> _incoming; // feeds the schedule
	private readonly ManualResetEventSlim _wake;                                    // wakes the producer
	private readonly Channel<IRecurringJob> _workChannel;                           // ready work for the pool
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
		_schedule = new PriorityQueue<IRecurringJob, DateTime>();
		_incoming = new ConcurrentQueue<(IRecurringJob, DateTime)>();
		_wake = new ManualResetEventSlim(false);
		_workChannel = Channel.CreateUnbounded<IRecurringJob>(new UnboundedChannelOptions
		{
			SingleWriter = true,  // only the producer writes ready jobs
			SingleReader = false  // the worker pool reads
		});
		_cancellationTokenSource = new CancellationTokenSource();
	}

	/// <summary>
	/// Register job
	/// </summary>
	/// <param name="job">job instance</param>
	/// <returns>true - if job is registered, otherwise false</returns>
	protected bool Register(IRecurringJob job)
	{
		bool added;
		lock (_jobsDictLocker)
		{
			added = RegisterUnsafe(job);
		}

		if (added) ScheduleAt(job, DateTime.Now); // a fresh job is eligible immediately
		return added;
	}

	/// <summary>
	/// Register a jobs
	/// </summary>
	/// <param name="jobs">jobs</param>
	/// <returns>count success registered jobs</returns>
	protected int Register(IEnumerable<IRecurringJob> jobs)
	{
		int success = 0;
		var added = new List<IRecurringJob>();
		lock (_jobsDictLocker)
		{
			foreach (IRecurringJob job in jobs)
			{
				if (RegisterUnsafe(job))
				{
					success++;
					added.Add(job);
				}
			}
		}

		var now = DateTime.Now;
		foreach (var job in added)
		{
			ScheduleAt(job, now);
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

			// Consumers: a fixed pool of workers drains the ready queue (bounds parallelism).
			int workerCount = Config.MaxParallelJobExecute;
			var workers = new Task[workerCount];
			for (int i = 0; i < workerCount; i++)
			{
				workers[i] = Task.Run(WorkerLoop);
			}
			_workers = workers;

			// Producer: pushes jobs to the ready queue when their next-run time arrives.
			_producerLoop = Task.Factory.StartNew(ScheduleLoop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}
	}

	#endregion

	#region Private

	/// <summary>Put a job into the schedule and wake the producer.</summary>
	private void ScheduleAt(IRecurringJob job, DateTime when)
	{
		_incoming.Enqueue((job, when));
		_wake.Set();
	}

	/// <summary>
	/// When the job becomes eligible again after a run. <c>null</c> means "never" (a one-shot job,
	/// <see cref="TimeSpan.MaxValue"/>). Short sleeps are floored to <see cref="Configuration.SleepAfterCheck"/>
	/// to keep the previous polling granularity (so a near-zero sleep can't hot-loop).
	/// </summary>
	private DateTime? NextRunTime(IRecurringJob job)
	{
		TimeSpan sleep = job.SleepDuration;
		if (sleep == TimeSpan.MaxValue) return null;
		if (sleep < Config.SleepAfterCheck) sleep = Config.SleepAfterCheck;
		return DateTime.Now + sleep;
	}

	/// <summary>
	/// Producer loop. Runs on a dedicated long-running thread: keeps the schedule heap fed from
	/// <see cref="_incoming"/>, dispatches jobs whose time has come, and sleeps until the next one
	/// is due (woken early when new work arrives).
	/// </summary>
	private void ScheduleLoop()
	{
		var token = _cancellationTokenSource.Token;
		try
		{
			while (!token.IsCancellationRequested)
			{
				// Reset BEFORE draining so a Set racing with our drain can't be lost.
				_wake.Reset();
				DrainIncoming();

				var now = DateTime.Now;
				while (_schedule.TryPeek(out _, out DateTime when) && when <= now)
				{
					DispatchIfReady(_schedule.Dequeue(), now);
				}

				int waitMs;
				if (_schedule.TryPeek(out _, out DateTime next))
				{
					TimeSpan delay = next - DateTime.Now;
					if (delay <= TimeSpan.Zero) continue; // became due while dispatching
					waitMs = (int)Math.Min(delay.TotalMilliseconds, MaxWait.TotalMilliseconds);
				}
				else
				{
					waitMs = Timeout.Infinite; // nothing scheduled - park until new work arrives
				}

				_wake.Wait(waitMs, token);
			}
		}
		catch (OperationCanceledException)
		{
			// scheduler is shutting down
		}
	}

	private void DrainIncoming()
	{
		while (_incoming.TryDequeue(out var item))
		{
			_schedule.Enqueue(item.job, item.when);
		}
	}

	/// <summary>Push a due job to the ready queue, or re-schedule it if it is not Ready yet.</summary>
	private void DispatchIfReady(IRecurringJob job, DateTime now)
	{
		if (job.State == State.Ready)
		{
			job.ResetState();
			_workChannel.Writer.TryWrite(job);
		}
		else
		{
			// the estimate was early; look again after a short delay
			_schedule.Enqueue(job, now + Config.SleepAfterCheck);
		}
	}

	/// <summary>
	/// Consumer loop. Each worker waits for a queued job, executes it, raises the result event and
	/// re-schedules the job for its next run. There are <see cref="Configuration.MaxParallelJobExecute"/>
	/// workers, so the parallelism is bounded without a per-job Task dispatch.
	/// </summary>
	private async Task WorkerLoop()
	{
		var token = _cancellationTokenSource.Token;
		try
		{
			while (!token.IsCancellationRequested)
			{
				IRecurringJob job = await _workChannel.Reader.ReadAsync(token).ConfigureAwait(false);

				Interlocked.Increment(ref _inProgressCount);
				try
				{
					var res = await job.ExecuteAsync().ConfigureAwait(false);
					JobResultEvent?.Invoke(ref job, ref res);
				}
				finally
				{
					Interlocked.Decrement(ref _inProgressCount);
					DateTime? next = NextRunTime(job);
					if (next.HasValue) ScheduleAt(job, next.Value);
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

		// let the producer and workers observe the cancellation and finish before freeing anything
		try { _producerLoop?.Wait(); } catch { /* ignore shutdown errors */ }
		try { if (_workers is { } workers) Task.WaitAll(workers); } catch { /* ignore shutdown errors */ }

		_cancellationTokenSource.Dispose();
		_wake.Dispose();
		_workers = null;
	}
}
