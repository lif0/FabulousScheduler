# General concepts

A conceptual overview of how FabulousScheduler works. For the exact API of the shared
primitives see **[Core.md](Core.md)**; for each subsystem see
**[Recurring.md](Recurring.md)** and **[QueueBased.md](QueueBased.md)**.

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

A **job** is a unit of work that the scheduler runs for you. Every job carries the same
identity (the `IJob` contract — see [Core.md](Core.md#ijob)): a `Guid ID`, a `Name`, and
the `LastExecute` / `LastSuccessExecute` timestamps.

There are two flavours of job, each with its own subsystem:

| Flavour | Interface | Runs… | Docs |
|---------|-----------|-------|------|
| Recurring | `IRecurringJob` | repeatedly, sleeping `SleepDuration` after each **successful** run | [Recurring.md](Recurring.md) |
| Queue-based | `IQueueJob` | once per enqueue, in FIFO order | [QueueBased.md](QueueBased.md) |

> The key design rule: **a job never throws to signal failure.** Instead it returns a
> `JobResult<JobOk, JobFail>`. If the user delegate throws, the exception is caught and
> wrapped into the `JobFail` side of the result.

---

## Job result (`JobResult<Ok, Fail>`) <a id="jobresult" />

Every run returns a `JobResult<TOk, TFail>` — a small discriminated union that holds
**either** a success value **or** a failure value, never both, and never uses exceptions
for control flow.

```csharp
JobResult<JobOk, JobFail> result = /* ... */;

// Side effect per branch
result.Do(
    success: ok   => Console.WriteLine("{0} succeeded", ok.JobID),
    failure: fail => Console.WriteLine("{0} failed: {1}", fail.JobID, fail.Message)
);

// Map both branches to a value
string msg = result.Match(
    success: ok   => $"{ok.JobID} succeeded",
    failure: fail => $"{fail.JobID} failed: {fail.Message}"
);
```

The full method surface (`Do`, `Match`, `MatchAsync`, `GetFail`, `IsSuccess`/`IsFail`,
`JobID`) and the implicit conversions are documented in **[Core.md](Core.md#jobresult)**.

Each subsystem ships its own concrete `JobOk` / `JobFail` carrying a subsystem-specific
`Reason` enum:

| Subsystem | Ok type | Fail type | Reason enum |
|-----------|---------|-----------|-------------|
| Recurring | `Recurring.Result.JobOk` | `Recurring.Result.JobFail` (plain class) | `JobFailEnum` |
| Queue | `Queue.Result.JobOk` | `Queue.Result.JobFail` (**derives from `Exception`**) | `QueueJobFailEnum` |

> ⚠️ Note the asymmetry: the queue `JobFail` is itself an `Exception`, while the recurring
> `JobFail` is a plain class. Treat them as result objects in both cases.

---

## Usage <a id="usage" />

### A Job <a id="ajob" />

You normally never implement `IRecurringJob` / `IQueueJob` by hand. Instead you hand a
delegate to a manager's `Register(...)` method and the library wraps it into a job:

- `Action` — a synchronous delegate.
- `Func<Task>` — an asynchronous delegate.

The wrapper catches any exception thrown by your delegate and turns it into a `JobFail`
with `Reason = InternalException`. If you need full control over the job type (custom
fields, custom result logic) you can subclass `BaseRecurringJob` / `BaseQueueJob` — see
the "Make my own job manager" sections in the per-subsystem docs.

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

Static, process-wide facade for the queue subsystem. It needs an `IQueue` implementation
(the built-in one is `InMemoryQueue`):

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
