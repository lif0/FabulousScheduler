namespace FabulousScheduler.Queue;

public class Config
{
	public Config(int maxParallelJobExecute)
	{
		MaxParallelJobExecute = maxParallelJobExecute;
	}

	public int MaxParallelJobExecute { get; }

	/// <summary>
	/// Default configs
	/// </summary>
	/// <para><see cref="MaxParallelJobExecute"/> is ProcessorCount * 2</para>
	public static Config Default =>
		new(maxParallelJobExecute: Environment.ProcessorCount * 2);
}