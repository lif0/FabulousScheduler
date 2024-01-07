using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Interfaces;

public interface ICronJob: IJob
{
	public string Name { get; }
	public string Category { get; }
	//public string Description { get; }
	public CronJobStateEnum State { get; }
	public TimeSpan SleepDuration { get; }
	public long TotalRun { get; }
	public long TotalFail { get; }
	
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();

	public void SetStateWaiting();
}