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
    }

    [Fact]
    public async void Time_FailOne()
    {
        const int oneTimeJobMs = 100;

        var conf = new Config(1, TimeSpan.FromMilliseconds(50));
        CronJobManager.Init(conf);
        TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
        int countCall = 0;

        CronJobManager.CallbackEvent += r =>
        {
	        _testOutputHelper.WriteLine("${0} IsFail: {1}", r.Id, r.IsFail);
	        Interlocked.Increment(ref countCall);
	        tcs.SetResult(r);
	        tcs.TrySetCanceled();
        };


        CronJobManager.Register(
	        action: async () =>
	        {
		        await Task.Delay(oneTimeJobMs);
		        throw new Exception("some error");
	        }, TimeSpan.MaxValue
	    );
        
        var result = await tcs.Task;
        Assert.NotNull(result.GetFail());
        Assert.True(result.IsFail);
        Assert.Equal(1, countCall);
        Assert.Equal("some error", result.GetFail()?.Message);
    }
    
    [Fact]
    public async void Time_SuccessOne()
    {
	    const int oneTimeJobMs = 100;

	    var conf = new Config(1, TimeSpan.FromMilliseconds(50));
	    CronJobManager.Init(conf);
	    TaskCompletionSource<JobResult<JobOk, JobFail>> tcs = new();
	    int countCall = 0;

	    CronJobManager.CallbackEvent += r =>
	    {
		    _testOutputHelper.WriteLine("${0} IsSuccess: {1}", r.Id, r.IsSuccess);
		    Interlocked.Increment(ref countCall);
		    tcs.SetResult(r);
		    tcs.TrySetCanceled();
	    };


	    CronJobManager.Register(
		    action: async () =>
		    {
			    await Task.Delay(oneTimeJobMs);
		    }, TimeSpan.MaxValue
	    );
        
	    var result = await tcs.Task;
	    Assert.Null(result.GetFail());
	    Assert.True(result.IsSuccess);
	    Assert.Equal(1, countCall);
    }
}