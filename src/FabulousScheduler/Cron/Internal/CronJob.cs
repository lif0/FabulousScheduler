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

    protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
    {
        try
        {
            await _action.Invoke();
        }
        catch (Exception e)
        {
            // return Task.FromResult<JobResult<JobOk, JobFail>>(
            //     new JobFail(this.Id, CronJobFailEnum.InternalException, e.Message, e)
            // );
            return new JobFail(this.Id, CronJobFailEnum.InternalException, e.Message, e);
        }

        //return Task.FromResult<JobResult<JobOk, JobFail>>();
        return new JobOk(this.Id);
    }
}