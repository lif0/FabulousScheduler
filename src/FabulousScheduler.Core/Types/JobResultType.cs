using FabulousScheduler.Core.Interfaces.Result;
using System.Runtime.CompilerServices;

namespace FabulousScheduler.Core.Types;

/// <summary> The result of a job </summary>
/// <typeparam name="TOk"> Valid job result type </typeparam>
/// <typeparam name="TFail"> An exception or invalid job result type </typeparam>
public class JobResult<TOk, TFail> where TOk : IJobOk where TFail : IJobFail
{
    private readonly TOk? _value;
    private readonly TFail? _fail;

    public bool IsFail { get; }
    public bool IsSuccess => !IsFail;

    /// <summary> Creates a successful job result </summary>
    private JobResult(TOk value)
    {
        IsFail = false;
        _value = value;
        _fail = default;
    }
	
    /// <summary> Creates a failed job result </summary>
    private JobResult(TFail value)
    {
        IsFail = true;
        _fail = value;
        _value = default;
    }
    
    /// <summary> Converts a successful job result implicitly </summary>
    public static implicit operator JobResult<TOk,TFail>(TOk value) => new(value);
    
    /// <summary> Converts a failed job result implicitly </summary>
    public static implicit operator JobResult<TOk, TFail>(TFail fail) => new(fail);
    
    /// <summary> Matches job result and returns a tuple of results </summary>
    public (TResult, TFailResult) Match<TResult, TFailResult>(Func<TOk?, TFail?, (TResult, TFailResult)> f) => f(_value, _fail);

    /// <summary> Gets the job's identity </summary>
    /// <returns> The job's identity </returns>
    public Guid JobID => !IsFail ? _value!.ID : _fail!.ID;

    /// <summary> Returns the failing job result if any </summary>
    public TFail? GetFail() => _fail;

    /// <summary> Matches the job result with success or failure functions and returns a value </summary>
    public TResult Match<TResult>(
        Func<TOk, TResult> success,
        Func<TFail, TResult> failure) =>
        !IsFail ? success(_value!) : failure(_fail!);

    /// <summary> Asynchronously matches the job result with success or failure functions and returns a value </summary>
    public Task<TResult> MatchAsync<TResult>(
        Func<TOk, Task<TResult>> success,
        Func<TFail, Task<TResult>> failure) =>
        !IsFail ? success(_value!) : failure(_fail!);

    /// <summary> Executes an action depending on whether the job result is successful or failed </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Do(Action<TOk> success, Action<TFail> failure)
    {
        if (IsFail)
        {
            failure(_fail!);
        }
        else
        {
            success(_value!);
        }
    }
}