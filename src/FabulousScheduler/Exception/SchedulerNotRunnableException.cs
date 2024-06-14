using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Exception;

/// <summary> You must register some jobs only after call <see cref="IJobScheduler.RunScheduler"/> /> </summary>
public class SchedulerNotRunnableException : System.Exception
{
    public SchedulerNotRunnableException(string nameOfScheduler) :
        base($"The scheduler is not runnable. To fix this, you need to call the {nameOfScheduler}.RunScheduler method before registering any jobs.") 
    { }
}