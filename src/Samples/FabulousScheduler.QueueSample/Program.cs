using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;

var queue = new InMemoryQueue(1);

var config = new Configuration(
    maxParallelJobExecute: 5
);
QueueJobManager.SetConfig(config, queue);

// Start a job scheduler
QueueJobManager.RunScheduler();

// Register callback for job's result
QueueJobManager.JobResultEvent += (ref IQueueJob job, ref JobResult<JobOk, JobFail> res) =>
{
    var now = DateTime.Now;
    if (res.IsSuccess)
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2}", now, job.Name, res.JobID);
    else
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2}", now, job.Name, res.JobID);
};

// Register a job
for (var i = 0; i < 5; i++)
{
    QueueJobManager.Register(
        action: () =>
        {
            //do some work
            int a = 10;
            int b = 100;
            int c = a + b;
            _ = c;
        },
        name: $"ExampleJob_{i}"
    );
}

/*
 * The job will fall asleep for 1 second after success completion, then it will wake up and will be push the job pool
*/

Thread.Sleep(-1);