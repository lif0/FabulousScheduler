using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Abstraction;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Internal;

internal sealed class CronJob : BaseCronJob
{
    private const string DefaultJobName = "anonimouse";
    private const string DefaultJobCategory = "internal";
    
    private readonly Func<Task>? _actionAsync;
    private readonly Action? _actionSync;

    public CronJob(Func<Task> action, string? name, string? category, TimeSpan sleepDuration) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, sleepDuration, true)
    {
        _actionAsync = action;
    }
    
    public CronJob(Action action, string? name, string? category, TimeSpan sleepDuration) : base(name ?? DefaultJobName, category ?? DefaultJobCategory, sleepDuration, false)
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
            return new JobFail(CronJobFailEnum.InternalException, base.ID, e.Message, e);
        }

        return new JobOk(base.ID);
    }
}