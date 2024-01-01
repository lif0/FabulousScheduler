using Job.Core.Interfaces.Result;
using JobScheduler.Queue.Enums;

namespace JobScheduler.Queue.Result;

public class JobFail : Exception, IJobFail  
{
	public JobFail(Guid id, string name, ReasonFail reason, string message, Exception? exception)
	{
		Id = id;
		Name = name;
		Reason = reason;
		Exception = exception;
	}

	public Guid Id { get; }
	public string Name { get; }
	public ReasonFail Reason { get; }

	public Exception? Exception { get; }

}