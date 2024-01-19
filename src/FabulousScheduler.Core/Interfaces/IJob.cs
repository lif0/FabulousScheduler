namespace FabulousScheduler.Core.Interfaces;

/// <summary>
/// Instance a job
/// </summary>
public interface IJob : IDisposable, IAsyncDisposable
{
	/// <summary>
	/// Job's identity key 
	/// </summary>
	public Guid ID { get; }
	
	/// <summary>
	/// Job's name
	/// </summary>
	public string Name { get; }
	
	/// <summary>
	/// Last time when a job was executed
	/// </summary>
	/// <remarks>Null - the job haven't been ever run </remarks>
	public DateTime? LastExecute { get; }
	
	/// <summary>
	/// Last time when the job was success executed
	/// </summary>
	/// <remarks>Null - a job haven't been ever run </remarks>
	public DateTime? LastSuccessExecute { get; }
}