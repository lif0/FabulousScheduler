using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Queue.Enums;

namespace FabulousScheduler.Queue.Result;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobFail : Exception, IJobFail  
{
	public JobFail(Guid id, string name, QueueJobFailEnum reason, string message, Exception? exception): base(message)
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