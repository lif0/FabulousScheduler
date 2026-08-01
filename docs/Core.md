# Core (`FabulousScheduler.Core`)

`FabulousScheduler.Core` holds the pieces both subsystems share. There is no scheduling logic
in here, only the contracts (`IJob`, `IJobScheduler`, `IJobOk`, `IJobFail`) and the
`JobResult<TOk, TFail>` type.

If you want the big picture first, read [General concepts](General.md).

### 📖 Contents

- [IJob](#ijob)
- [IJobScheduler](#ijobscheduler)
- [IJobOk / IJobFail](#results)
- [JobResult&lt;TOk, TFail&gt;](#jobresult)

---

## IJob <a id="ijob" />

`FabulousScheduler.Core.Interfaces.IJob` is the identity every job carries. It also implements
`IDisposable` and `IAsyncDisposable`.

| Member | Type | Meaning |
|--------|------|---------|
| `ID` | `Guid` | Unique id, generated when the job is created. |
| `Name` | `string` | A readable name. If you don't pass one, it defaults to `"anonymous"`. |
| `LastExecute` | `DateTime?` | When the job last ran, success or not. `null` until the first run. |
| `LastSuccessExecute` | `DateTime?` | When the job last ran successfully. `null` until the first success. |

Each subsystem has its own interface on top of `IJob`:

- `IRecurringJob` adds `Category`, `State`, `SleepDuration`, `TotalRun`, `TotalFail` and
  `ExecuteAsync()`. See [Recurring.md](Recurring.md).
- `IQueueJob` adds `State`, `TotalRun`, `Attempts`, `ExecuteAsync()` and `ResetState()`.
  See [QueueBased.md](QueueBased.md).

---

## IJobScheduler <a id="ijobscheduler" />

`FabulousScheduler.Core.Interfaces.IJobScheduler` is the smallest scheduler contract. It
implements `IDisposable`.

```csharp
public interface IJobScheduler : IDisposable
{
    void RunScheduler();   // start the scheduler
}
```

`IRecurringJobScheduler` and `IQueueJobScheduler` both extend it and add a `JobResultEvent`
(with a `JobResultEventHandler` delegate) that fires after every run, using the job and result
types of their subsystem.

---

## IJobOk / IJobFail <a id="results" />

These are the two markers a result can hold. Each one exposes the job `ID` and nothing else.
The real payload (a message, a reason, the original exception) lives on the concrete types that
each subsystem ships.

```csharp
public interface IJobOk   { Guid ID { get; } }
public interface IJobFail { Guid ID { get; } }
```

| Subsystem | Ok type | Fail type | Extra members on the fail type |
|-----------|---------|-----------|--------------------------------|
| Recurring | `Recurring.Result.JobOk` | `Recurring.Result.JobFail` | `Reason` (`JobFailEnum`), `Message`, `Exception?` |
| Queue | `Queue.Result.JobOk` | `Queue.Result.JobFail` | `Reason` (`QueueJobFailEnum`), `Message`, `Exception?` |

Both `JobFail` types are plain result objects. When a job fails, you read the reason, the
message and (if there was one) the original exception off the object. You never throw it.

---

## JobResult&lt;TOk, TFail&gt; <a id="jobresult" />

`FabulousScheduler.Core.Types.JobResult<TOk, TFail>` (`where TOk : IJobOk`,
`where TFail : IJobFail`) is a tiny either/or value. It carries a success value or a failure
value, one of the two, and it does not use exceptions to signal which.

### Creating a result

You rarely call a constructor. A `JobOk` or a `JobFail` converts into a result on its own:

```csharp
JobResult<JobOk, JobFail> ok   = new JobOk(id);                  // becomes a success
JobResult<JobOk, JobFail> fail = new JobFail(reason, id, "msg"); // becomes a failure
```

### Inspecting

| Member | Type | Meaning |
|--------|------|---------|
| `IsSuccess` | `bool` | `true` when it holds a `TOk`. |
| `IsFail` | `bool` | `true` when it holds a `TFail`. |
| `JobID` | `Guid` | The job's `ID`, whatever the outcome. |
| `GetFail()` | `TFail?` | The failure, or `null` on success. |

### Consuming

| Method | Returns | What it's for |
|--------|---------|---------------|
| `Do(Action<TOk> success, Action<TFail> failure)` | `void` | a side effect per branch |
| `Match<TResult>(Func<TOk,TResult> success, Func<TFail,TResult> failure)` | `TResult` | turn both branches into one value |
| `Match<TResult,TFailResult>(Func<TOk?,TFail?,(TResult,TFailResult)> f)` | `(TResult, TFailResult)` | a low-level tuple projection |
| `MatchAsync<TResult>(Func<TOk,Task<TResult>>, Func<TFail,Task<TResult>>)` | `Task<TResult>` | the async version of `Match` |

```csharp
// A side effect per branch
result.Do(
    success: ok   => Console.WriteLine("{0} succeeded", ok.JobID),
    failure: fail => Console.WriteLine("{0} failed: {1}", fail.JobID, fail.Message)
);

// Map to a value
string msg = result.Match(
    success: ok   => $"{ok.JobID} succeeded",
    failure: fail => $"{fail.JobID} failed: {fail.Message}"
);

// Async
int code = await result.MatchAsync(
    success: async ok   => { await Task.Yield(); return 0; },
    failure: async fail => { await Task.Yield(); return 1; }
);
```

One thing to watch: every `JobResult` is a class, so it allocates. With a lot of jobs per second
that adds up. There are numbers for it in [Benchmarks](Benchmarks.md#jobresult).
