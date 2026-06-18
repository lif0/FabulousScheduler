# Core (`FabulousScheduler.Core`)

The `FabulousScheduler.Core` package holds the shared primitives that both subsystems
build on. It contains **no scheduling logic** — only the contracts (`IJob`,
`IJobScheduler`, `IJobOk`, `IJobFail`) and the `JobResult<TOk, TFail>` type.

For the high-level picture see **[General concepts](General.md)**.

### 📖 Contents

- [IJob](#ijob)
- [IJobScheduler](#ijobscheduler)
- [IJobOk / IJobFail](#results)
- [JobResult&lt;TOk, TFail&gt;](#jobresult)

---

## IJob <a id="ijob" />

`FabulousScheduler.Core.Interfaces.IJob` is the identity every job shares. It extends
`IDisposable` and `IAsyncDisposable`.

| Member | Type | Meaning |
|--------|------|---------|
| `ID` | `Guid` | Unique identifier, generated when the job is created. |
| `Name` | `string` | Human-readable name. Defaults to `"anonimouse"` when not supplied. |
| `LastExecute` | `DateTime?` | Last time the job ran (any outcome). `null` if it has never run. |
| `LastSuccessExecute` | `DateTime?` | Last time the job ran **successfully**. `null` if it has never succeeded. |

The two subsystem interfaces extend `IJob` and add their own members:

- `IRecurringJob` — adds `Category`, `State`, `SleepDuration`, `TotalRun`, `TotalFail`,
  `ExecuteAsync()`. See [Recurring.md](Recurring.md).
- `IQueueJob` — adds `State`, `TotalRun`, `Attempts`, `ExecuteAsync()`, `ResetState()`.
  See [QueueBased.md](QueueBased.md).

---

## IJobScheduler <a id="ijobscheduler" />

`FabulousScheduler.Core.Interfaces.IJobScheduler` is the minimal scheduler contract; it
extends `IDisposable`.

```csharp
public interface IJobScheduler : IDisposable
{
    void RunScheduler();   // start the scheduler
}
```

Both `IRecurringJobScheduler` and `IQueueJobScheduler` extend it and add a
`JobResultEventHandler` delegate / `JobResultEvent` for result callbacks (with subsystem
-specific job and result types).

---

## IJobOk / IJobFail <a id="results" />

The two markers carried by a result. Both expose only the job `ID`; concrete payloads
(message, reason, exception) are added by the subsystem-specific implementations.

```csharp
public interface IJobOk   { Guid ID { get; } }
public interface IJobFail { Guid ID { get; } }
```

| Subsystem | Ok type | Fail type | Extra members on the fail type |
|-----------|---------|-----------|--------------------------------|
| Recurring | `Recurring.Result.JobOk` | `Recurring.Result.JobFail` (plain class) | `Reason` (`JobFailEnum`), `Message`, `Exception?` |
| Queue | `Queue.Result.JobOk` | `Queue.Result.JobFail` (**`: System.Exception`**) | `Reason` (`QueueJobFailEnum`), `Exception?` |

> ⚠️ Design asymmetry: the queue `JobFail` derives from `Exception`, the recurring one is
> a plain class. Use them as result objects, not as things to `throw`.

---

## JobResult&lt;TOk, TFail&gt; <a id="jobresult" />

`FabulousScheduler.Core.Types.JobResult<TOk, TFail>`
(`where TOk : IJobOk`, `where TFail : IJobFail`) is a small discriminated union. It holds
**either** a success value **or** a failure value, never both, and never uses exceptions
for control flow.

### Creating a result

A result is created implicitly from either side — you rarely call a constructor:

```csharp
JobResult<JobOk, JobFail> ok   = new JobOk(id);                  // implicit -> success
JobResult<JobOk, JobFail> fail = new JobFail(reason, id, "msg"); // implicit -> failure
```

### Inspecting

| Member | Type | Meaning |
|--------|------|---------|
| `IsSuccess` | `bool` | `true` when it holds a `TOk`. |
| `IsFail` | `bool` | `true` when it holds a `TFail`. |
| `JobID` | `Guid` | The job's `ID`, regardless of outcome. |
| `GetFail()` | `TFail?` | The failure, or `null` on success. |

### Consuming

| Method | Returns | Use it to… |
|--------|---------|------------|
| `Do(Action<TOk> success, Action<TFail> failure)` | `void` | run a side effect per branch |
| `Match<TResult>(Func<TOk,TResult> success, Func<TFail,TResult> failure)` | `TResult` | map both branches to one value |
| `Match<TResult,TFailResult>(Func<TOk?,TFail?,(TResult,TFailResult)> f)` | `(TResult, TFailResult)` | low-level tuple projection |
| `MatchAsync<TResult>(Func<TOk,Task<TResult>>, Func<TFail,Task<TResult>>)` | `Task<TResult>` | async mapping |

```csharp
// Side effect per branch
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
