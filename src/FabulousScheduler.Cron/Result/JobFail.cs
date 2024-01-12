using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Cron.Enums;

namespace FabulousScheduler.Cron.Result;

/// <summary>
/// The result of successful completion of a job
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobFail : IJobFail
{
	public Guid Id { get; }
	
	public JobFail(Guid jobId, CronJobFailEnum reason, string message, Exception? exception)
	{
		Id = jobId;
		Reason = reason;
		Message = message;

		Exception = exception;
		
	}

	public CronJobFailEnum Reason { get; }
	
	public string Message { get; }
	
	public Exception? Exception { get; }
}