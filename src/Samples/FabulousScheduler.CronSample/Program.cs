using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron;
using FabulousScheduler.Cron.Result;

var config = new Config(
    maxParallelJobExecute: 5,
    sleepAfterCheck: TimeSpan.FromMilliseconds(100)
);

CronJobManager.Init(config);
CronJobManager.JobResultEvent += (ref ICronJob job, ref JobResult<JobOk, JobFail> res) =>
{
    var now = DateTime.Now;
    if (res.IsSuccess)
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} IsSuccess", now, job.Name, res.ID);
    else
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} IsFail", now, job.Name, res.ID);
};

CronJobManager.Register(
    action: () =>
    {
        //do some work
        int a = 10;
        int b = 100;
        int c = a + b;
        
        return Task.CompletedTask;
    },
    sleepDuration: TimeSpan.FromSeconds(1),
    name: "ExampleJob"
);

Thread.Sleep(-1);