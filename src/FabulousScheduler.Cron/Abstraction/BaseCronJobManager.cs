using System.Collections.Concurrent;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Abstraction;

public abstract class BaseCronJobManager : ICronJobManager
{
	private readonly object _lock;
	private readonly SemaphoreSlim _jobParallelPool;

	protected readonly ConcurrentDictionary<string, ICronJob> Jobs;

	protected BaseCronJobManager(int maxParallelJob)
	{
		Jobs = new ConcurrentDictionary<string, ICronJob>();
		_jobParallelPool = new SemaphoreSlim(maxParallelJob, maxParallelJob);
		_lock = new object();
	}
	
	/// <summary>
	/// Register job
	/// </summary>
	/// <param name="job">job instance</param>
	/// <returns>true - if job is registered, otherwise false</returns>
	protected bool Register(ICronJob job)
	{
		return Jobs.TryAdd(job.Name, job);
	}
	
	/// <summary>
	/// Register a jobs
	/// </summary>
	/// <param name="job">jobs</param>
	/// <returns>count success registered jobs</returns>
	protected int Register(IEnumerable<ICronJob> jobs)
	{
		int fail = 0;
		foreach (var job in jobs)
		{
			if (!this.Jobs.TryAdd(job.Name, job))
			{
				fail++;
			}
		}

		return fail;
	}

	protected Task<JobResult<JobOk, JobFail>[]> ExecuteReadyJob()
	{
		ICronJob[] jobs;
		lock(_lock)
		{
			jobs = Jobs
				.Where(x => x.Value.State == CronJobStateEnum.Ready)
				.Select(x => x.Value)
				.OrderBy(x => x.LastExecute)
				.ToArray();

			if (jobs.Length == 0)
			{
				return Task.FromResult(Array.Empty<JobResult<JobOk, JobFail>>());
			}

			Parallel.ForEach(jobs, j => j.SetStateWaiting());
		}

		return ExecuteJobsAsync(jobs);
	}

	#region Public
	public int CurrentRunnableJobCount() => _jobParallelPool.CurrentCount;
	#endregion
	
	#region Private

	private async Task<JobResult<JobOk, JobFail>[]> ExecuteJobsAsync(ICronJob[] jobs)
	{
		var results = new ConcurrentBag<JobResult<JobOk, JobFail>>();

		var tasks = new Task[jobs.Length];
		int i = 0;

		foreach (var job in jobs)
		{
			await _jobParallelPool.WaitAsync();

			tasks[i] = Task.Factory.StartNew(async obj =>
			{
				var cronJob = obj as ICronJob;
				if (cronJob is null)
				{
					ArgumentNullException.ThrowIfNull(cronJob);
				}

				//TODO :> кажется этот момент делает жесткую подству
				var res = await cronJob.ExecuteAsync();
				//var res = cronJob.ExecuteAsync().GetAwaiter().GetResult();
				_jobParallelPool.Release();

				return res;
			}, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

			i++;
		}

		//Task.WaitAll(tasks);
		await Task.WhenAll(tasks);

		return results.ToArray();
	}

	#endregion
}