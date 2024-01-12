using FabulousScheduler.Core.Interfaces.Result;

namespace FabulousScheduler.Cron.Result;

public class JobOk : IJobOk
{
    public Guid ID { get; }

    public JobOk(Guid jobId)
    {
        ID = jobId;
    }
}