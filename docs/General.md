# General concepts

How FabulousScheduler fits together. For the exact API of the shared pieces see
[Core.md](Core.md); for each subsystem see [Recurring.md](Recurring.md) and
[QueueBased.md](QueueBased.md).

### 📖 Contents

- [What is a job](#mainidea)
- [Job result (JobResult&lt;Ok,Fail&gt;)](#jobresult)
- [Usage](#usage)
    - [A Job](#ajob)
    - [RecurringJobManager](#recurringjobmanager)
    - [QueueJobManager](#queuejobmanager)
- [Benchmarks](#benchmarks)

---

## What is a job <a id="mainidea" />

A job is one piece of work the scheduler runs for you. Whatever kind it is, it carries the same
identity (the `IJob` contract, see [Core.md](Core.md#ijob)): a `Guid ID`, a `Name`, and the
`LastExecute` / `LastSuccessExecute` timestamps.

There are two kinds, and each one has its own subsystem:

| Kind | Interface | When it runs | Docs |
|------|-----------|--------------|------|
| Recurring | `IRecurringJob` | over and over, sleeping `SleepDuration` after each successful run | [Recurring.md](Recurring.md) |
| Queue-based | `IQueueJob` | once per enqueue, in FIFO order | [QueueBased.md](QueueBased.md) |

A job never throws to report a failure. It returns a `JobResult<JobOk, JobFail>` instead. If your
own delegate throws, the library catches it and puts it on the `JobFail` side of the result, so a
bug in one job can't take down the run.

---

## Job result (`JobResult<Ok, Fail>`) <a id="jobresult" />

Every run hands back a `JobResult<TOk, TFail>`. It holds a success value or a failure value, one
of the two, and it doesn't use exceptions to say which.

```csharp
JobResult<JobOk, JobFail> result = /* ... */;

// A side effect per branch
result.Do(
    success: ok   => Console.WriteLine("{0} succeeded", ok.JobID),
    failure: fail => Console.WriteLine("{0} failed: {1}", fail.JobID, fail.Message)
);

// Or turn both branches into one value
string msg = result.Match(
    success: ok   => $"{ok.JobID} succeeded",
    failure: fail => $"{fail.JobID} failed: {fail.Message}"
);
```

The rest of the surface (`Do`, `Match`, `MatchAsync`, `GetFail`, `IsSuccess`/`IsFail`, `JobID`)
and the implicit conversions are in [Core.md](Core.md#jobresult).

Each subsystem ships its own `JobOk` / `JobFail` with a `Reason` enum that fits that subsystem:

| Subsystem | Ok type | Fail type | Reason enum |
|-----------|---------|-----------|-------------|
| Recurring | `Recurring.Result.JobOk` | `Recurring.Result.JobFail` | `JobFailEnum` |
| Queue | `Queue.Result.JobOk` | `Queue.Result.JobFail` | `QueueJobFailEnum` |

Both `JobFail` types are plain classes that hold the reason, a message and the original exception
(when there is one). Treat them as data you read, not as something to `throw`.

---

## Usage <a id="usage" />

### A Job <a id="ajob" />

Most of the time you don't implement `IRecurringJob` / `IQueueJob` yourself. You pass a delegate
to a manager's `Register(...)` and the library wraps it into a job for you:

- `Action` for synchronous work,
- `Func<Task>` for asynchronous work.

The wrapper catches anything your delegate throws and turns it into a `JobFail` with
`Reason = InternalException`. When you need more (extra fields, your own result logic), subclass
`BaseRecurringJob` / `BaseQueueJob` instead. The "Make my own job manager" sections in the
per-subsystem docs show how.

### RecurringJobManager <a id="recurringjobmanager" />

A static, process-wide entry point for recurring jobs:

```csharp
RecurringJobManager.SetConfig(config);     // optional, only before RunScheduler
RecurringJobManager.RunScheduler();        // start it
RecurringJobManager.JobResultEvent += ...; // listen for results
Guid id = RecurringJobManager.Register(action, name, category, sleepDuration);
```

The state machine, configuration and the rest live in [Recurring.md](Recurring.md).

### QueueJobManager <a id="queuejobmanager" />

The same idea for the queue subsystem, except it needs an `IQueue` (the built-in one is
`InMemoryQueue`):

```csharp
var queue = new InMemoryQueue();
QueueJobManager.SetConfig(config, queue);  // optional config, only before RunScheduler
QueueJobManager.RunScheduler();            // start it
QueueJobManager.JobResultEvent += ...;     // listen for results
Guid id = QueueJobManager.Register(action, name, category);
```

More in [QueueBased.md](QueueBased.md).

Both managers hold you to two rules. Calling `Register(...)` before `RunScheduler()` throws
`SchedulerNotRunnableException`, and calling `SetConfig(...)` after `RunScheduler()` throws
`SetConfigAfterRunSchedulingException`.

---

## Benchmarks <a id="benchmarks" />

There's a separate page with numbers: how much a `JobResult` costs, what one job costs to start,
how the scheduler picks the next job, and the end-to-end throughput of both schedulers. See
[Benchmarks.md](Benchmarks.md).
