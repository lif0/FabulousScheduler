using System.Diagnostics.CodeAnalysis;

namespace FabulousScheduler.Cron;
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public class Config
{
    public Config(int maxParallelJobExecute, TimeSpan sleepAfterCheck)
    {
        MaxParallelJobExecute = maxParallelJobExecute;

        if (sleepAfterCheck == TimeSpan.Zero || sleepAfterCheck == TimeSpan.MinValue)
        {
            SleepAfterCheck = TimeSpan.FromMilliseconds(10);
        }
        else
        {
            SleepAfterCheck = sleepAfterCheck;
        }
		
    }

    /// <summary>
    /// Max parallel job executing
    /// </summary>
    public int MaxParallelJobExecute { get; }
    
    /// <summary>
    /// Time the loop is await after last checks jobs
    /// </summary>
    public TimeSpan SleepAfterCheck { get; }

    /// <summary>
    /// Default configs
    /// </summary>
    /// <para><see cref="MaxParallelJobExecute"/> is ProcessorCount * 2</para>
    /// <para><see cref="SleepAfterCheck"/> is 1 sec</para>
    public static Config Default =>
        new(
            maxParallelJobExecute: Environment.ProcessorCount * 2,
            sleepAfterCheck: TimeSpan.FromSeconds(1)
        );
}