using FabulousScheduler.Core.Interfaces;

namespace FabulousScheduler.Exception;

/// <summary> You must set config before <see cref="IJobScheduler.RunScheduler"/> </summary>
public class SetConfigAfterRunSchedulingException : System.Exception
{
    public SetConfigAfterRunSchedulingException(string nameOfScheduler) 
        : base($"Can't set config, because {nameOfScheduler} already initialized")
    { }
}