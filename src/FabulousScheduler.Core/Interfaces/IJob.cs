namespace FabulousScheduler.Core.Interfaces;

public interface IJob : IDisposable, IAsyncDisposable
{
	public Guid Id { get; }
	public DateTime? LastExecute { get; }
	public DateTime? LastSuccessExecute { get; }
}