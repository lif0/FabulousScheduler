using FabulousScheduler.Cron;

CronJobManager.Init();
CronJobManager.CallbackEvent += result =>
{
    Console.WriteLine("[{0:hh:mm:ss}] jobID: {1} IsSuccess: {2}", DateTime.Now, result.ID, result.IsSuccess, result.IsFail);
};

CronJobManager.Register(
    action: () =>
    {
        Console.WriteLine();
        throw new Exception("some err");
    },
    sleepDuration: TimeSpan.FromSeconds(1)
);

Thread.Sleep(-1);