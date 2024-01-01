using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Cron.Enums;

namespace FabulousScheduler.Cron.Result;

public class JobFail : IJobFail
{
	public JobFail(CronJobFailEnum reason, string message, Exception? exception)
	{
		Reason = reason;
		Message = message;

		Exception = exception;
		
	}

	public CronJobFailEnum Reason { get; }
	
	public string Message { get; }
	
	public Exception? Exception { get; }

}