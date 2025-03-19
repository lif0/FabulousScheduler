using FabulousScheduler.Recurring.Abstraction;
using FabulousScheduler.Recurring.Result;
using FabulousScheduler.Recurring.Enums;
using FabulousScheduler.Core.Types;

namespace FabulousScheduler.Recurring.Internal;

internal sealed class RecurringJob : BaseRecurringJob
{
    private const string DefaultJobName = "anonimouse";
    private const string DefaultJobCategory = "internal";
    
    private readonly Func<Task>? _actionAsync;
    private readonly Action? _actionSync;

    public RecurringJob(Func<Task> action, string? name, string? category, TimeSpan sleepDuration) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, sleepDuration, true)
    {
        _actionAsync = action;
    }
    
    public RecurringJob(Action action, string? name, string? category, TimeSpan sleepDuration) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, sleepDuration, false)
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
            return new JobFail(JobFailEnum.InternalException, base.ID, e.Message, e);
        }

        return new JobOk(base.ID);
    }
}