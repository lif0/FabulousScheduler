using System.Collections.Concurrent;
using JobScheduler.Cron.Enums;
using JobScheduler.Cron.Interfaces;
using JobScheduler.Cron.Result;
using Job.Core.Types;

namespace JobScheduler.Cron.Abstraction;

public abstract class BaseCronJobManager : ICronJobManager
{
	private readonly object _lock;
	private readonly SemaphoreSlim _jobPool;

	protected readonly ConcurrentDictionary<string, ICronJob> Jobs;

	protected BaseCronJobManager(int maxParallelJob)
	{
		Jobs = new ConcurrentDictionary<string, ICronJob>();
		_jobPool = new SemaphoreSlim(maxParallelJob, maxParallelJob);
		_lock = new object();
	}
	
	/// <summary>
	/// Register job
	/// </summary>
	/// <param name="job">job instance</param>
	/// <returns>true - if jos is registered, otherwise false</returns>
	protected bool Register(ICronJob job)
	{
		return Jobs.TryAdd(job.Name, job);
	}
	
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
			jobs = this.Jobs
				.Where(x => x.Value.State == CronJobStateEnum.Ready)
				.Select(x => x.Value)
				.OrderBy(x => x.LastExecute)
				.ToArray();

			if (jobs.Length == 0)
			{
				return Task.FromResult(Array.Empty<JobResult<JobOk, JobFail>>());
			}

			Parallel.ForEach(jobs, j => j.SetWaiting());
		}

		return ExecuteJobsAsync(jobs);
	}

	#region Public
	public int CurrentRunnableJobCount() => _jobPool.CurrentCount;
	#endregion
	
	#region Private

	private async Task<JobResult<JobOk, JobFail>[]> ExecuteJobsAsync(ICronJob[] jobs)
	{
		var results = new ConcurrentBag<JobResult<JobOk, JobFail>>();

		var tasks = new Task[jobs.Length];
		int i = 0;
		
		foreach (var job in jobs)
		{
			await _jobPool.WaitAsync();

			tasks[i] = Task.Factory.StartNew(async obj =>
			{
				var @job = obj as ICronJob;
				if (@job is null)
				{
					ArgumentNullException.ThrowIfNull(@job);
				}

				var res = await @job.ExecuteAsync();
				_jobPool.Release();

				return res;
			}, job, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

			i++;
		}

		Task.WaitAll(tasks);

		return results.ToArray();
	}

	#endregion
}