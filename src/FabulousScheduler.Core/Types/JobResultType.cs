namespace FabulousScheduler.Core.Types;

/// <summary>
/// Result a job
/// </summary>
/// <typeparam name="TOk">Valid JobJobResult</typeparam>
/// <typeparam name="TFail">Exception or not valid JobJobResult</typeparam>
public class JobResult<TOk, TFail>
{
    private readonly TOk? _value;
    private readonly TFail? _fail;

    public bool IsFail { get; }
    public bool IsSuccess => !IsFail;

    private JobResult(TOk value)
    {
        IsFail = false;
        _value = value;
        _fail = default;
    }
	
    private JobResult(TFail value)
    {
        IsFail = true;
        _fail = value;
        _value = default;
    }
	

    public static implicit operator JobResult<TOk,TFail>(TOk value) => new(value);
    public static implicit operator JobResult<TOk,TFail>(TFail fail) => new(fail);

    public (TResult, TFailResult) Match<TResult, TFailResult>(Func<TOk?, TFail?, (TResult, TFailResult)> f) => f(_value, _fail);
	
    public TFail? GetFail() => _fail;
	
    public TResult Match<TResult>(
        Func<TOk, TResult> success,
        Func<TFail, TResult> failure) =>
        !IsFail ? success(_value!) : failure(_fail!);

    public Task<TResult> MatchAsync<TResult>(
        Func<TOk, Task<TResult>> success,
        Func<TFail, Task<TResult>> failure) =>
        !IsFail ? success(_value!) : failure(_fail!);
}