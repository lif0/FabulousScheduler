global using Xunit;

// Cron
global using Job_Random  = Job.Core.Tests.Cron.CronJobRandomResult;
global using Job_Ok      = Job.Core.Tests.Cron.CronJobOkResult;
global using Job_Fail    = Job.Core.Tests.Cron.CronJobFailedExecuteResult;
global using Job_FailExp = Job.Core.Tests.Cron.CronJobInternalExceptionResult;

// Queue
global using QJob_Random     = Job.Core.Tests.Queue.QueueRandomJob;
global using QJob_Ok         = Job.Core.Tests.Queue.QueueJobOkResult;
global using QJob_Fail       = Job.Core.Tests.Queue.QueueJobFailResult;
global using QJob_FailExp    = Job.Core.Tests.Queue.QueueJobFailExceptionResult;
global using QJob_Attempts       = Job.Core.Tests.Queue.QueueJobAttemptsFailNextOk;