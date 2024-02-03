using Xunit.Abstractions;
using FabulousScheduler.Cron;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Interfaces;
using FabulousScheduler.Cron.Result;

// ReSharper disable AsyncVoidLambda
namespace Job.Core.Tests.Cron;

public class DefaultJobManagerTests
{
	private readonly ITestOutputHelper _testOutputHelper;

	static DefaultJobManagerTests()
    {
	    var conf = new Config(1, TimeSpan.FromMilliseconds(50));
	    CronJobManager.SetConfig(conf);
	    CronJobManager.RunScheduler();
    }

	public DefaultJobManagerTests(ITestOutputHelper testOutputHelper)
	{
		_testOutputHelper = testOutputHelper;
	}

	[Fact]
    public async void Time_FailOne()
    {
	    // config
        const int oneTimeJobMs = 100;
        Guid jobID = Guid.Empty;
        
        // helper
        TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
        int countCall = 0;
        
        void OnCallbackEvent(ref ICronJob sender, ref JobResult<JobOk, JobFail> e)
        {
	        // ReSharper disable once AccessToModifiedClosure
	        if (e.JobID != jobID) return;

	        _testOutputHelper.WriteLine("${0} IsFail: {1} {2}", e.JobID, e.IsFail, DateTime.Now.Ticks);
	        Interlocked.Increment(ref countCall);
	        tcs.SetResult(e);
	        tcs.TrySetCanceled();
        }
        CronJobManager.JobResultEvent += OnCallbackEvent;

        // test
        jobID = CronJobManager.Register(
	        action: async () =>
	        {
		        await Task.Delay(oneTimeJobMs);
		        throw new Exception("some error"); // because is error, job.State will be Ready forever!
	        }, TimeSpan.MaxValue
        );
        var result = await tcs.Task;
        CronJobManager.JobResultEvent -= OnCallbackEvent;

        Assert.NotNull(result.GetFail());
        Assert.True(result.IsFail);
        Assert.Equal(1, countCall);
        Assert.Equal("some error", result.GetFail()?.Message);
    }

    [Fact]
    public async void Time_SuccessOne()
    {
	    // config
	    const int oneTimeJobMs = 100;
	    var jobID = Guid.Empty;
	    
	    // helper
	    TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
	    int countCall = 0;
	    
	    void OnCallbackEvent(ref ICronJob sender, ref JobResult<JobOk, JobFail> e)
	    {
		    // ReSharper disable once AccessToModifiedClosure
		    if (e.JobID != jobID) return;

		    _testOutputHelper.WriteLine("${0} IsSuccess: {1} {2}", e.JobID, e.IsSuccess, DateTime.Now.Ticks);
		    countCall++;
		    tcs.SetResult(e);
		    tcs.TrySetCanceled();
	    }
	    CronJobManager.JobResultEvent += OnCallbackEvent;
	    
	    // test
	    jobID = CronJobManager.Register(
		    action: async () =>
		    {
			    await Task.Delay(oneTimeJobMs);
		    }, TimeSpan.MaxValue
	    );
	    var result = await tcs.Task;
	    CronJobManager.JobResultEvent -= OnCallbackEvent;

	    Assert.Null(result.GetFail());
	    Assert.True(result.IsSuccess);
	    Assert.Equal(1, countCall);
    }

}