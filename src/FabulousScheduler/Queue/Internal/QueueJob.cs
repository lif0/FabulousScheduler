using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Enums;

namespace FabulousScheduler.Queue.Internal;

internal sealed class QueueJob : BaseQueueJob
{
    private const string DefaultJobName = "anonimouse";
    private const string DefaultJobCategory = "internal";
    
    private readonly Func<Task>? _actionAsync;
    private readonly Action? _actionSync;

    public QueueJob(Func<Task> action, string? name, string? category, byte? attempts) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, true, attempts)
    {
        _actionAsync = action;
    }
    
    public QueueJob(Action action, string? name, string? category, byte? attempts) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, false, attempts)
    {
        _actionSync = action;
    }

    protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
    {
        try
        {
            if (IsAsyncAction)
            {
                await _actionAsync!.Invoke();
            }
            else
            {
                _actionSync!.Invoke();
            }
        }
        catch (System.Exception e)
        {
            return new JobFail(QueueJobFailEnum.InternalException, base.ID, e.Message, e);
        }

        return new JobOk(base.ID, base.Name);
    }
}