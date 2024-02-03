using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

BenchmarkRunner.Run<MethodCoverTask>();

[SimpleJob(RuntimeMoniker.Net70)]
[MemoryDiagnoser]
public class MethodCoverTaskEmptyAction
{
    private CoverSyncAndAsyncMethodTask syncMethodInternal = new(() => { });
    
    private CoverSyncAndAsyncMethodTask asyncMethodInternal = new(() => Task.CompletedTask);
    
    [Benchmark]
    public void AsyncCall()
    {
        asyncMethodInternal.ActionJobTask().GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public void AsyncCallWithTryCatch()
    {
        asyncMethodInternal.ActionJobTaskWithTryCatch().GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public void SyncCall()
    { 
        syncMethodInternal.ActionJob();
    }
    
    [Benchmark]
    public void SyncCallWithTryCatch()
    { 
        syncMethodInternal.ActionJobWithTryCatch();
    }
}

[SimpleJob(RuntimeMoniker.Net70)]
[MemoryDiagnoser]
public class MethodCoverTask
{
    private SHA256 sha256 = SHA256.Create();
    private byte[] data;
    
    [Params(1000, 10000)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        data = new byte[N];
        new Random(42).NextBytes(data);
        
        
        syncMethodInternal =  new(() =>
        {
            sha256.ComputeHash(data);
        });
        
        asyncMethodInternal  = new(()  =>
        {
            sha256.ComputeHash(data);
            return Task.CompletedTask;
        });
    }


    private CoverSyncAndAsyncMethodTask syncMethodInternal;

    private CoverSyncAndAsyncMethodTask asyncMethodInternal;
    
    [Benchmark]
    public void AsyncCall()
    {
        asyncMethodInternal.ActionJobAggregatorWithTryCatch().GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public void AsyncCallWithTryCatch()
    {
        asyncMethodInternal.ActionJobAggregatorWithTryCatch().GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public void SyncCall()
    {
        syncMethodInternal.ActionJobAggregatorWithTryCatch().GetAwaiter().GetResult();
    }
    
    [Benchmark]
    public void SyncCallWithTryCatch()
    { 
        syncMethodInternal.ActionJobAggregatorWithTryCatch().GetAwaiter().GetResult();
    }
}


public class CoverSyncAndAsyncMethodTask
{
    private readonly Func<Task>? _actionAsync;
    private readonly Action? _actionSync;
    
    public CoverSyncAndAsyncMethodTask(Action? actionSync)
    {
        _actionSync = actionSync;
    }
    
    public CoverSyncAndAsyncMethodTask(Func<Task>? actionAsync)
    {
        _actionAsync = actionAsync;
    }

    public async Task ActionJobTask()
    {
        await _actionAsync!.Invoke();
    }
    
    public async Task ActionJobTaskWithTryCatch()
    {
        try
        {
            await _actionAsync!.Invoke();
        }
        catch (System.Exception e)
        {
            throw;
        }
    }
    
    public void ActionJob()
    {
        _actionSync!.Invoke();
    }
    
    public void ActionJobWithTryCatch()
    {
        try
        {
            _actionSync!.Invoke();
        }
        catch (System.Exception e)
        {
            throw;
        }
    }
    
    public async Task ActionJobAggregatorWithTryCatch()
    {
        try
        {
            if (_actionSync != null)
            {
                _actionSync!.Invoke();
            }
            else
            {
                await _actionAsync!.Invoke();
            }
        }
        catch (System.Exception e)
        {
            throw;
        }
    }
}