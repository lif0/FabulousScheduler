using FabulousScheduler.Core.Interfaces.Result;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Core.Interfaces;

/// <summary>
/// Job scheduler
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Callback delegate, that will be call every time when any job will completed
    /// </summary>
    /// <param name="eResult">Job result</param>
    //protected delegate void CallbackHandler(JobResult<IJobOk, IJobFail> eResult);
}