using FabulousScheduler.Core.Types;
using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Queue.Enums;
using Xunit.Abstractions;

// ReSharper disable AsyncVoidLambda
namespace Job.Core.Tests.Queue;

public class DefaultJobManagerTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	static DefaultJobManagerTests()
	{
		var conf = new Configuration(maxParallelJobExecute: 1);
		QueueJobManager.SetConfig(conf, new InMemoryQueue());
		QueueJobManager.RunScheduler();
	}

	public DefaultJobManagerTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
	public async Task Time_FailOne()
	{
		// config
		const int oneTimeJobMs = 100;
		Guid jobID = Guid.Empty;

		// helper
		TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
		int countCall = 0;

		void OnCallbackEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> e)
		{
			// ReSharper disable once AccessToModifiedClosure
			if (e.JobID != jobID) return;

			_testOutputHelper.WriteLine("${0} IsFail: {1} {2}", e.JobID, e.IsFail, DateTime.Now.Ticks);
			Interlocked.Increment(ref countCall);
			tcs.SetResult(e);
			tcs.TrySetCanceled();
		}
		QueueJobManager.JobResultEvent += OnCallbackEvent;

		// test
		jobID = QueueJobManager.Register(
			action: async () =>
			{
				await Task.Delay(oneTimeJobMs);
				throw new Exception("some error");
			}
		);
		var result = await tcs.Task;
		QueueJobManager.JobResultEvent -= OnCallbackEvent;

		Assert.NotNull(result.GetFail());
		Assert.True(result.IsFail);
		Assert.Equal(1, countCall);
		Assert.Equal(QueueJobFailEnum.InternalException, result.GetFail()?.Reason);
		Assert.Equal("some error", result.GetFail()?.Message);
	}

	[Fact]
	public async Task Time_SuccessOne()
	{
		// config
		const int oneTimeJobMs = 100;
		var jobID = Guid.Empty;

		// helper
		TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
		int countCall = 0;

		void OnCallbackEvent(ref IQueueJob sender, ref JobResult<JobOk, JobFail> e)
		{
			// ReSharper disable once AccessToModifiedClosure
			if (e.JobID != jobID) return;

			_testOutputHelper.WriteLine("${0} IsSuccess: {1} {2}", e.JobID, e.IsSuccess, DateTime.Now.Ticks);
			countCall++;
			tcs.SetResult(e);
			tcs.TrySetCanceled();
		}
		QueueJobManager.JobResultEvent += OnCallbackEvent;

		// test
		jobID = QueueJobManager.Register(
			action: async () =>
			{
				await Task.Delay(oneTimeJobMs);
			}
		);
		var result = await tcs.Task;
		QueueJobManager.JobResultEvent -= OnCallbackEvent;

		Assert.Null(result.GetFail());
		Assert.True(result.IsSuccess);
		Assert.Equal(1, countCall);
	}
}
