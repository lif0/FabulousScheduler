# General concepts

### 📖 Contents

- [What is a job](#mainidea)
- [Job result (JobResult&lt;Ok,Fail&gt;)](#jobresult)
- [Usage](#usage)
    - [A Job](#ajob)
    - [RecurringJobManager](#recurringjobmanager)
    - [QueueJobManager](#queuejobmanager)
- [Benchmarks](#benchmarks)
    - [Performance](#performance)

---

## What is a job <a id="mainidea" />

A **job** is a unit of work that the scheduler runs for you. Every job implements the
`IJob` interface (`FabulousScheduler.Core.Interfaces.IJob`):

| Member | Type | Meaning |
|--------|------|---------|
| `ID` | `Guid` | Unique identifier, generated when the job is created. |
| `Name` | `string` | Human-readable name. Defaults to `"anonimouse"` when not supplied. |
| `LastExecute` | `DateTime?` | Last time the job ran (any outcome). `null` if it has never run. |
| `LastSuccessExecute` | `DateTime?` | Last time the job ran **successfully**. `null` if it has never succeeded. |

`IJob` also extends `IDisposable` and `IAsyncDisposable`.

There are two flavours of job, each with its own subsystem:

| Flavour | Interface | Runs… | Docs |
|---------|-----------|-------|------|
| Recurring | `IRecurringJob` | repeatedly, sleeping `SleepDuration` after each **successful** run | [Recurring.md](Recurring.md) |
| Queue-based | `IQueueJob` | once per enqueue, in FIFO order | [QueueBased.md](QueueBased.md) |

> The key design rule: **a job never throws to signal failure.** Instead it returns a
> `JobResult<JobOk, JobFail>`. If the user delegate throws, the exception is caught and
> wrapped into the `JobFail` side of the result (see below).

---

## Job result (`JobResult<Ok, Fail>`) <a id="jobresult" />

`FabulousScheduler.Core.Types.JobResult<TOk, TFail>` is a small discriminated union
(`where TOk : IJobOk`, `where TFail : IJobFail`). It carries **either** a success value
**or** a failure value, never both, and never uses exceptions for control flow.

```csharp
public bool IsSuccess { get; } // true  -> holds a TOk
public bool IsFail    { get; } // true  -> holds a TFail
public Guid JobID     { get; } // ID of the job, regardless of outcome
```

A result is created implicitly from either side:

```csharp
JobResult<JobOk, JobFail> ok   = new JobOk(id);                  // implicit -> success
JobResult<JobOk, JobFail> fail = new JobFail(reason, id, "msg"); // implicit -> failure
```

### Consuming a result

| Method | Returns | Use it to… |
|--------|---------|------------|
| `Do(Action<TOk> success, Action<TFail> failure)` | `void` | run a side effect for each branch |
| `Match<TResult>(Func<TOk,TResult> success, Func<TFail,TResult> failure)` | `TResult` | map both branches to one value |
| `Match<TResult,TFailResult>(Func<TOk?,TFail?,(TResult,TFailResult)> f)` | `(TResult, TFailResult)` | low-level tuple projection |
| `MatchAsync<TResult>(Func<TOk,Task<TResult>>, Func<TFail,Task<TResult>>)` | `Task<TResult>` | async mapping |
| `GetFail()` | `TFail?` | get the failure (or `null` on success) |

```csharp
JobResult<JobOk, JobFail> result = /* ... */;

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
```

### `IJobOk` / `IJobFail`

Both result subsystems provide their own concrete types, but they share the core markers:

- `IJobOk` — carries `ID` (the job's `Guid`).
- `IJobFail` — carries `ID`.

The concrete failure types add a `Reason` (an enum) and an optional `Exception`:

| Subsystem | Ok type | Fail type | Notes |
|-----------|---------|-----------|-------|
| Recurring | `FabulousScheduler.Recurring.Result.JobOk` | `FabulousScheduler.Recurring.Result.JobFail` | `JobFail` is a plain class with `Reason` (`JobFailEnum`), `Message`, `Exception?`. |
| Queue | `FabulousScheduler.Queue.Result.JobOk` | `FabulousScheduler.Queue.Result.JobFail` | `JobFail` **derives from `System.Exception`** and exposes `Reason` (`QueueJobFailEnum`) and `Exception?`. |

> ⚠️ Note the asymmetry: the queue `JobFail` is itself an `Exception`, while the recurring
> `JobFail` is a plain class. Treat them as result objects in both cases — do not rely on
> being able to `throw` one over the other.

---

## Usage <a id="usage" />

### A Job <a id="ajob" />

You normally never implement `IRecurringJob` / `IQueueJob` by hand. Instead you hand a
delegate to a manager's `Register(...)` method and the library wraps it into a job:

- `Action` — a synchronous delegate.
- `Func<Task>` — an asynchronous delegate.

The wrapper catches any exception thrown by your delegate and turns it into a
`JobFail` with `Reason = InternalException`. If you need full control over the job type
(custom fields, custom result logic) you can subclass `BaseRecurringJob` /
`BaseQueueJob` — see the per-subsystem docs.

### RecurringJobManager <a id="recurringjobmanager" />

Static, process-wide facade for the recurring subsystem. Lifecycle:

```csharp
RecurringJobManager.SetConfig(config);     // optional, must be BEFORE RunScheduler
RecurringJobManager.RunScheduler();        // start the loop
RecurringJobManager.JobResultEvent += ...; // subscribe to results
Guid id = RecurringJobManager.Register(action, name, category, sleepDuration);
```

Full details, the state machine and configuration: **[Recurring.md](Recurring.md)**.

### QueueJobManager <a id="queuejobmanager" />

Static, process-wide facade for the queue subsystem. It needs an `IQueue`
implementation (the built-in one is `InMemoryQueue`):

```csharp
var queue = new InMemoryQueue();
QueueJobManager.SetConfig(config, queue);  // optional config, must be BEFORE RunScheduler
QueueJobManager.RunScheduler();            // start the loop
QueueJobManager.JobResultEvent += ...;     // subscribe to results
Guid id = QueueJobManager.Register(action, name, category);
```

Full details: **[QueueBased.md](QueueBased.md)**.

> Both managers enforce two contracts:
> - `Register(...)` before `RunScheduler()` throws `SchedulerNotRunnableException`.
> - `SetConfig(...)` after `RunScheduler()` throws `SetConfigAfterRunSchedulingException`.

---

## Benchmarks <a id="benchmarks" />

_Planned._ Benchmarks live in `src/Benchmark/Common` (BenchmarkDotNet) and currently
measure the sync-vs-async delegate dispatch overhead used by `BaseRecurringJob` /
`BaseQueueJob`.

### Performance <a id="performance" />

_Planned._
