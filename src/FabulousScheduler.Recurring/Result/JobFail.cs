using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Recurring.Enums;

namespace FabulousScheduler.Recurring.Result;

/// <summary>
/// <inheritdoc cref="IJobFail"/>
/// </summary>
public sealed class JobFail : IJobFail
{
	public JobFail(JobFailEnum reason, Guid jobID, string message, Exception? exception = null)
	{
		ID = jobID;
		Reason = reason;
		Message = message;
		Exception = exception;
	}
	
	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public Guid ID { get; }

	/// <summary>
	/// Message
	/// </summary>
	public string Message { get; }

	/// <summary>
	/// <inheritdoc cref="JobFailEnum"/>
	/// </summary>
	public JobFailEnum Reason { get; }

	/// <summary>
	/// Exception
	/// </summary>
	public Exception? Exception { get; }
}