global using Xunit;

// Recurring
global using Job_Random  = Job.Core.Tests.Recurring.RecurringJobRandomResult;
global using Job_Ok      = Job.Core.Tests.Recurring.RecurringJobOkResult;
global using Job_Fail    = Job.Core.Tests.Recurring.RecurringJobFailedExecuteResult;
global using Job_FailExp = Job.Core.Tests.Recurring.RecurringJobInternalExceptionResult;

// Queue-based
global using QJob_Random     = Job.Core.Tests.Queue.QueueRandomJob;
global using QJob_Ok         = Job.Core.Tests.Queue.QueueJobOkResult;
global using QJob_Fail       = Job.Core.Tests.Queue.QueueJobFailResult;
global using QJob_FailExp    = Job.Core.Tests.Queue.QueueJobFailExceptionResult;
global using QJob_Attempts       = Job.Core.Tests.Queue.QueueJobAttemptsFailNextOk;