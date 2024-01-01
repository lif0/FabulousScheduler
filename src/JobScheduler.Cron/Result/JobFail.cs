using Job.Core.Interfaces.Result;
using JobScheduler.Cron.Enums;

namespace JobScheduler.Cron.Result;

public class JobFail : IJobFail
{
	public JobFail(ReasonFail reason, string message, Exception? exception)
	{
		Reason = reason;
		Message = message;

		Exception = exception;
		
	}

	public ReasonFail Reason { get; }
	
	public string Message { get; }
	
	public Exception? Exception { get; }

}