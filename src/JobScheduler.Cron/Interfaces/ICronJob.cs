using Job.Core.Interfaces;
using Job.Core.Types;
using JobScheduler.Cron.Result;

namespace JobScheduler.Cron.Interfaces;

public interface ICronJob: IJob
{
	public string Name { get; }
	public string Category { get; }
	//public string Description { get; }
	public State State { get; }
	public TimeSpan SleepDuration { get; }
	public long TotalRun { get; }
	public long TotalFail { get; }
	
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();

	public void SetWaiting();
}