using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Queue.Result;

/// <summary>
/// The result of successful completion of a job
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobOk : IJobOk
{
	public JobOk(Guid id, string name)
	{
		ID = id;
		Name = name;
	}

	/// <summary>
	/// Job's ID
	/// </summary>
	public Guid ID { get; }
	
	/// <summary>
	/// Job's Name
	/// </summary>
	public string Name { get; }
}