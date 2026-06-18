namespace FabulousScheduler.Core.Interfaces;

/// <summary> Instance a job </summary>
public interface IJob : IDisposable, IAsyncDisposable
{
	/// <summary> The job's unique identifier </summary>
	public Guid ID { get; }
	
	/// <summary> The job's name </summary>
	public string Name { get; }

	/// <summary> The last time the job was executed </summary>
	/// <remarks> Null - if the job has never been run </remarks>
	public DateTime? LastExecute { get; }
	
	/// <summary> The last time the job was executed successfully </summary>
	/// <remarks> Null if the job has never been executed successfully </remarks>
	public DateTime? LastSuccessExecute { get; }
}