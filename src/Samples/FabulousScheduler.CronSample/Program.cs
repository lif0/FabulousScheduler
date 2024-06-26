﻿using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Result;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron;

var config = new Configuration(
    maxParallelJobExecute: 5,
    sleepAfterCheck: TimeSpan.FromMilliseconds(100)
);
CronJobManager.SetConfig(config);

// Start a job scheduler
CronJobManager.RunScheduler();

// Register callback for job's result
CronJobManager.JobResultEvent += (ref ICronJob job, ref JobResult<JobOk, JobFail> res) =>
{
    var now = DateTime.Now;
    if (res.IsSuccess)
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2}", now, job.Name, res.JobID);
    else
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2}", now, job.Name, res.JobID);
};

// Register a job
CronJobManager.Register(
    action: () =>
    {
        //do some work
        int a = 10;
        int b = 100;
        int c = a + b;
        _ = c;
    },
    sleepDuration: TimeSpan.FromSeconds(1),
    name: "ExampleJob"
);

/*
 * The job will fall asleep for 1 second after success completion, then it will wake up and will be push the job pool
*/

Thread.Sleep(-1);