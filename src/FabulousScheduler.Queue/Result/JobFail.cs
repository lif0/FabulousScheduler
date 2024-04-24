using FabulousScheduler.Queue.Enums;
using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Queue.Result;

/// <summary>
///  <inheritdoc cref="IJobFail"/>
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobFail : Exception, IJobFail  
{
	public JobFail(QueueJobFailEnum reason, Guid jobID, string message, Exception? exception = null): base(message)
	{
		ID = jobID;
		Reason = reason;
		Exception = exception;
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public Guid ID { get; }

	/// <summary>
	/// <inheritdoc cref="QueueJobFailEnum"/>
	/// </summary>
	public QueueJobFailEnum Reason { get; }

	/// <summary>
	/// Exception
	/// </summary>
	public Exception? Exception { get; }
}