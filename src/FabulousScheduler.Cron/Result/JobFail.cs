using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Cron.Enums;

namespace FabulousScheduler.Cron.Result;

/// <summary>
/// <inheritdoc cref="IJobFail"/>
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class JobFail : IJobFail
{
	public JobFail(CronJobFailEnum reason, Guid jobID, string message, Exception? exception = null)
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
	/// <inheritdoc cref="CronJobFailEnum"/>
	/// </summary>
	public CronJobFailEnum Reason { get; }

	/// <summary>
	/// Exception
	/// </summary>
	public Exception? Exception { get; }
}