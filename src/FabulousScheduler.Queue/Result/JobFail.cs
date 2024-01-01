using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Queue.Enums;

namespace FabulousScheduler.Queue.Result;

public class JobFail : Exception, IJobFail  
{
	public JobFail(Guid id, string name, QueueJobFailEnum reason, string message, Exception? exception)
	{
		Id = id;
		Name = name;
		Reason = reason;
		Exception = exception;
	}

	public Guid Id { get; }
	public string Name { get; }
	public QueueJobFailEnum Reason { get; }

	public Exception? Exception { get; }

}