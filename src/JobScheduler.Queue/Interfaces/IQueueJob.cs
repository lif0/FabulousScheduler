using Job.Core.Interfaces;
using Job.Core.Types;
using JobScheduler.Queue.Result;

namespace JobScheduler.Queue.Interfaces;

public interface IQueueJob: IJob
{
	public Guid Id { get; }
	public string Name { get; }
	public ushort TotalRun { get; }
	public JobState State { get; }
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();
	public void SetWaiting();
}