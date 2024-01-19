using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Queue.Enums;

namespace FabulousScheduler.Queue.Result;

/// <summary>
///  <inheritdoc cref="IJobFail"/>
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobFail : Exception, IJobFail  
{
	public JobFail(Guid id, string name, QueueJobFailEnum reason, string message, Exception? exception): base(message)
	{
		ID = id;
		Name = name;
		Reason = reason;
		Exception = exception;
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public Guid ID { get; }
	
	/// <summary>
	/// <inheritdoc cref="IJob.Name"/>
	/// </summary>
	public string Name { get; }
	
	/// <summary>
	/// <inheritdoc cref="QueueJobFailEnum"/>
	/// </summary>
	public QueueJobFailEnum Reason { get; }

	/// <summary>
	/// Exception
	/// </summary>
	public Exception? Exception { get; }

}