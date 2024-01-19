using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Result;

namespace FabulousScheduler.Queue.Interfaces;

public interface IQueueJob: IJob
{
	public ushort TotalRun { get; }
	public QueueJobStateEnum State { get; }
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();
	public void SetWaiting();
}