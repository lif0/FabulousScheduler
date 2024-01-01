namespace JobScheduler.Queue;

public class Config
{
	public Config(int maxParallelJobExecute, TimeSpan sleepIfQueueIsEmpty)
	{
		MaxParallelJobExecute = maxParallelJobExecute;

		if (sleepIfQueueIsEmpty == TimeSpan.Zero || sleepIfQueueIsEmpty == TimeSpan.MinValue)
		{
			SleepIfQueueIsEmpty = TimeSpan.FromSeconds(10);
		}
		else
		{
			SleepIfQueueIsEmpty = sleepIfQueueIsEmpty;
		}
		
	}

	public int MaxParallelJobExecute { get; }
	public TimeSpan SleepIfQueueIsEmpty { get; }
}