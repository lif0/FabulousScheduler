using System.Diagnostics.CodeAnalysis;
using FabulousScheduler.Core.Interfaces;
using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Queue.Result;

/// <summary>
///  <inheritdoc cref="IJobOk"/>
/// </summary>
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class JobOk : IJobOk
{
	public JobOk(Guid id, string name)
	{
		ID = id;
		Name = name;
	}

	/// <inheritdoc cref="IJobOk.ID"/>
	public Guid ID { get; }
	
	/// <inheritdoc cref="IJob.Name"/>
	public string Name { get; }
}