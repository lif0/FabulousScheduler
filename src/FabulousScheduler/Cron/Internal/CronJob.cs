using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Abstraction;
using FabulousScheduler.Cron.Enums;
using FabulousScheduler.Cron.Result;

namespace FabulousScheduler.Cron.Internal;

internal sealed class CronJob : BaseCronJob
{
    private const string NameJob = "anonimouse";
    private const string CategoryJob = "internal";
    
    private readonly Func<Task> _action;

    public CronJob(Func<Task> action, TimeSpan sleepDuration) : base(NameJob, CategoryJob, sleepDuration)
    {
        _action = action;
    }
    
    public CronJob(Func<Task> action, string name, TimeSpan sleepDuration) : base(name, CategoryJob, sleepDuration)
    {
        _action = action;
    }
    
    public CronJob(Func<Task> action, string name, string category, TimeSpan sleepDuration) : base(name, category, sleepDuration)
    {
        _action = action;
    }

    protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
    {
        try
        {
            await _action.Invoke();
        }
        catch (Exception e)
        {
            return new JobFail(this.ID, CronJobFailEnum.InternalException, e.Message, e);
        }

        return new JobOk(this.ID);
    }
}