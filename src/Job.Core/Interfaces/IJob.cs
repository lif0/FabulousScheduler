namespace Job.Core.Interfaces;

public interface IJob : IDisposable, IAsyncDisposable
{ 
	public DateTime? LastExecute { get; }
	public DateTime? LastSuccessExecute { get; }
}