namespace FabulousScheduler.Queue;

public class Configuration
{
	/// <summary> Default configs </summary>
	/// <para><see cref="MaxParallelJobExecute"/> is ProcessorCount * 2</para>
	public static Configuration Default =>
		new(maxParallelJobExecute: Environment.ProcessorCount * 2);

	/// <summary> Create Configuration instance </summary>
	/// <param name="maxParallelJobExecute"> Maximum job executing in parallel </param>
	public Configuration(int maxParallelJobExecute)
	{
		MaxParallelJobExecute = maxParallelJobExecute;
	}

	public int MaxParallelJobExecute { get; }
}