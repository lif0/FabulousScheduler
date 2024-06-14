using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Enums;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Queue.Interfaces;

public interface IQueueJob: IJob
{
	public uint TotalRun { get; }
	public byte? Attempts { get; }
	public QueueJobStateEnum State { get; }
	
	public Task<JobResult<JobOk, JobFail>> ExecuteAsync();
	public void ResetState();
}