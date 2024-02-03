using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Abstraction;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Internal;

internal sealed class CronJob : BaseCronJob
{
    private const string NameJob = "anonimouse";
    private const string CategoryJob = "internal";
    
    private readonly Func<Task>? _actionAsync;
    private readonly Action? _actionSync;

    public CronJob(Func<Task> action, TimeSpan sleepDuration) : base(NameJob, CategoryJob, sleepDuration, true)
    {
        _actionAsync = action;
    }

    public CronJob(Func<Task> action, string name, TimeSpan sleepDuration) : base(name, CategoryJob, sleepDuration, true)
    {
        _actionAsync = action;
    }
    
    public CronJob(Func<Task> action, string name, string category, TimeSpan sleepDuration) : base(name, category, sleepDuration, true)
    {
        _actionAsync = action;
    }
    
    public CronJob(Action action, TimeSpan sleepDuration) : base(NameJob, CategoryJob, sleepDuration, false)
    {
        _actionSync = action;
    }
    
    public CronJob(Action action, string name, TimeSpan sleepDuration) : base(name, CategoryJob, sleepDuration, false)
    {
        _actionSync = action;
    }
    
    public CronJob(Action action, string name, string category, TimeSpan sleepDuration) : base(name, category, sleepDuration, false)
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
            return new JobFail(this.ID, CronJobFailEnum.InternalException, e.Message, e);
        }

        return new JobOk(this.ID);
    }
}