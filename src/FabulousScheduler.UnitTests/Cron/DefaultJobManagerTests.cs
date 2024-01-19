using Xunit.Abstractions;
using FabulousScheduler.Cron;
using FabulousScheduler.Core.Types;
using FabulousScheduler.Cron.Result;

// ReSharper disable AsyncVoidLambda
namespace Job.Core.Tests.Cron;

public class DefaultJobManagerTests
{
	private readonly ITestOutputHelper _testOutputHelper;

    public DefaultJobManagerTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        
        var conf = new Config(1, TimeSpan.FromMilliseconds(50));
        CronJobManager.Init(conf);
    }
    
    [Fact]
    public async void Time_FailOne()
    {
        const int oneTimeJobMs = 100;
        Guid jobID = Guid.Empty;
        TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
        int countCall = 0;
        
        void OnCallbackEvent(JobResult<JobOk, JobFail> r)
        {
	        if (r.ID != jobID) return;

	        _testOutputHelper.WriteLine("${0} IsFail: {1} {2}", r.ID, r.IsFail, DateTime.Now.Ticks);
	        Interlocked.Increment(ref countCall);
	        tcs.SetResult(r);
	        tcs.TrySetCanceled();
        }
        CronJobManager.CallbackEvent += OnCallbackEvent;
        
        jobID = CronJobManager.Register(
	        action: async () =>
	        {
		        await Task.Delay(oneTimeJobMs);
		        throw new Exception("some error"); // because is error, job.State will be Ready forever!
	        }, TimeSpan.MaxValue
        );

        var result = await tcs.Task;
        CronJobManager.CallbackEvent -= OnCallbackEvent;
        Assert.NotNull(result.GetFail());
        Assert.True(result.IsFail);
        Assert.Equal(1, countCall);
        Assert.Equal("some error", result.GetFail()?.Message);
    }

    [Fact]
    public async void Time_SuccessOne()
    {
	    const int oneTimeJobMs = 100;
	    Guid jobID = Guid.Empty;
	    TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
	    int countCall = 0;
	    
	    void OnCallbackEvent(JobResult<JobOk, JobFail> r)
	    {
		    if (r.ID != jobID) return;

		    _testOutputHelper.WriteLine("${0} IsSuccess: {1} {2}", r.ID, r.IsSuccess, DateTime.Now.Ticks);
		    countCall++;
		    tcs.SetResult(r);
		    tcs.TrySetCanceled();
	    }
	    CronJobManager.CallbackEvent += OnCallbackEvent;


	    jobID = CronJobManager.Register(
		    action: async () =>
		    {
			    await Task.Delay(oneTimeJobMs);
		    }, TimeSpan.MaxValue
	    );
        
	    var result = await tcs.Task;
	    CronJobManager.CallbackEvent -= OnCallbackEvent;
	    Assert.Null(result.GetFail());
	    Assert.True(result.IsSuccess);
	    Assert.Equal(1, countCall);
    }
}