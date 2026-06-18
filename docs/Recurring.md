# Recurring Scheduler

Recurring jobs run again and again. After a **successful** run a job sleeps for its
`SleepDuration` and then becomes eligible to run again.

### 📖 Contents

- [Default Job Manager](#default)
- [Make my own job manager](#myself)
- [Usage](#usage)
    - [BaseRecurringJob](#baserecurringjob)
    - [BaseRecurringScheduler](#baserecurringshceduler)
    - [Example](#example)
- [Benchmarks](#benchmarks)
    - [Performance](#performance)[QueueScheduler.md](QueueScheduler.md)

---

## Default Job Manager <a id="default" />

`RecurringJobManager` (in `FabulousScheduler.Recurring`) is a static, process-wide
facade. The required call order is:

```
1. SetConfig(config)   // OPTIONAL — only before RunScheduler
2. RunScheduler()      // start the background loop
3. JobResultEvent +=   // subscribe to results
4. Register(...)       // add jobs (only AFTER RunScheduler)
```

> ⚠️ The order matters and is enforced:
> - calling `Register(...)` before `RunScheduler()` throws `SchedulerNotRunnableException`;
> - calling `SetConfig(...)` after `RunScheduler()` throws `SetConfigAfterRunSchedulingException`.

### Register overloads

`Register` returns the new job's `Guid`. Both synchronous (`Action`) and asynchronous
(`Func<Task>`) delegates are supported, with optional `name` and `category`:

```csharp
Guid Register(Action     action,                                 TimeSpan sleepDuration);
Guid Register(Action     action, string name,                    TimeSpan sleepDuration);
Guid Register(Action     action, string name, string category,   TimeSpan sleepDuration);
Guid Register(Func<Task> action,                                 TimeSpan sleepDuration);
Guid Register(Func<Task> action, string name,                    TimeSpan sleepDuration);
Guid Register(Func<Task> action, string name, string category,   TimeSpan sleepDuration);
```

Defaults when omitted: `name = "anonimouse"`, `category = "internal"`.

### The result callback

```csharp
RecurringJobManager.JobResultEvent +=
    (ref IRecurringJob job, ref JobResult<JobOk, JobFail> res) =>
    {
        if (res.IsSuccess)
            Console.WriteLine("{0} ok", job.Name);
        else
            Console.WriteLine("{0} failed: {1}", job.Name, res.GetFail()!.Message);
    };
```

The delegate uses `ref` parameters to avoid copying when the result is forwarded.

### State machine (`JobStateEnum`)

```
Ready ──scheduler picks it──► Waiting ──dequeued──► Running
  ▲                                                    │
  │                                                    │ run finished
  │   SleepDuration elapsed (measured from the         ▼
  └────────── last SUCCESS) ─────────────────────── Sleeping

  Any state ──Dispose()──► Disposed
```

How `Sleeping → Ready` is decided (see `BaseRecurringJob.UpdateState`):

| `SleepDuration` | Behaviour after a run |
|-----------------|-----------------------|
| `TimeSpan.Zero` **or** `TimeSpan.MinValue` | Treated as zero — the job becomes `Ready` again immediately. |
| `TimeSpan.MaxValue` | The job never leaves `Sleeping` — effectively a run-once job. |
| any other value | The job becomes `Ready` once `now > LastSuccessExecute + SleepDuration`. |

> **Important behavioural detail.** `SleepDuration` is measured from the **last
> successful** execution, not from the last execution. A run that **fails** does not
> start a fresh sleep window, so a job that keeps failing is rescheduled again
> immediately and will busy-run until it succeeds. Build a delay / circuit breaker into
> your delegate if that is not what you want.

### Fail reasons (`JobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | `ExecuteAsync` was called while the job was not `Ready`/`Waiting`. |
| `InternalException` | The user delegate threw — the exception is in `JobFail.Exception`. |
| `FailedExecute` | The job explicitly returned a `JobFail`. |
| `Disposed` | The job was disposed before execution. |

---

## Make my own job manager <a id="myself" />

`RecurringJobManager` is just a thin static wrapper around two public base classes. When
you need more control — custom job state, your own registration store, dependency
injection instead of a static singleton — subclass them directly.

**1. A custom job** — derive from `BaseRecurringJob` and implement `ActionJob()`:

```csharp
using FabulousScheduler.Recurring.Abstraction;
using FabulousScheduler.Recurring.Result;
using FabulousScheduler.Core.Types;

public sealed class MyRecurringJob : BaseRecurringJob
{
    // isAsyncAction tells the base whether ActionJob awaits real work.
    public MyRecurringJob(string name, string category, TimeSpan sleepDuration)
        : base(name, category, sleepDuration, isAsyncAction: true) { }

    protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
    {
        // ... your work ...
        await Task.Delay(10);

        return new JobOk(ID);                 // success
        // return new JobFail(JobFailEnum.FailedExecute, ID, "reason"); // failure
    }
}
```

**2. A custom scheduler** — derive from `BaseRecurringScheduler` and expose registration.
The base class gives you a `protected bool Register(IRecurringJob)` (and an
`IEnumerable<IRecurringJob>` overload), the background loop, and the `JobResultEvent`:

```csharp
using FabulousScheduler.Recurring.Abstraction;

public sealed class MyRecurringScheduler : BaseRecurringScheduler
{
    public MyRecurringScheduler(Configuration? config) : base(config) { }

    public Guid Add(MyRecurringJob job)
    {
        base.Register(job);   // adds the job to the internal registry
        return job.ID;
    }
}
```

Usage mirrors the default manager: `new MyRecurringScheduler(config)` → subscribe to
`JobResultEvent` → `RunScheduler()` → `Add(job)`.

---

## Usage <a id="usage" />

### BaseRecurringJob <a id="baserecurringjob" />

`public abstract class BaseRecurringJob : IRecurringJob`. A thread-safe state machine
that delegates the actual work to the abstract `ActionJob()`.

| Member | Description |
|--------|-------------|
| `ID`, `Name`, `Category` | Identity (see [Core.md](Core.md)). |
| `SleepDuration` | Sleep window after a successful run. `Zero`/`MinValue` are normalised to `Zero` in the constructor. |
| `State` | Current `JobStateEnum`. Reading it re-evaluates the `Sleeping → Ready` transition. |
| `TotalRun` | `ulong` — number of times the job started. |
| `TotalFail` | `ulong` — number of failed runs. |
| `LastExecute` / `LastSuccessExecute` | Timestamps (nullable). |
| `Task<JobResult<JobOk,JobFail>> ExecuteAsync()` | Runs the job once and updates state/counters. |
| `protected abstract Task<JobResult<JobOk,JobFail>> ActionJob()` | Your work. Return `JobOk`/`JobFail`. |
| `Dispose()` / `DisposeAsync()` | Marks the job disposed. `DisposeAsync` delegates to `Dispose`. |

Constructor:

```csharp
protected BaseRecurringJob(string name, string category, TimeSpan sleepDuration, bool isAsyncAction);
```

### BaseRecurringScheduler <a id="baserecurringshceduler" />

`public abstract class BaseRecurringScheduler : IRecurringJobScheduler`. Runs one
`LongRunning` background loop.

| Member | Description |
|--------|-------------|
| `event JobResultEventHandler JobResultEvent` | Fired after each job run with `(ref IRecurringJob, ref JobResult<JobOk,JobFail>)`. |
| `void RunScheduler()` | Starts the loop. Idempotent — a second call is a no-op. |
| `int CurrentRunnableJobCount()` | Number of jobs currently in flight. |
| `protected bool Register(IRecurringJob job)` | Registers one job; returns `false` if the `ID` is already present. |
| `protected int Register(IEnumerable<IRecurringJob> jobs)` | Registers many; returns the count actually added. |
| `void Dispose()` | Requests cancellation and releases resources. |

How the loop works: it scans the registry for jobs in `Ready` state (ordered by
`LastExecute`), moves them to `Waiting`, and enqueues them. A `SemaphoreSlim` caps the
number of concurrent runs at `Configuration.MaxParallelJobExecute`. When nothing is
ready it sleeps for `Configuration.SleepAfterCheck` before polling again.

### Configuration

`FabulousScheduler.Recurring.Configuration`:

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `Environment.ProcessorCount * 10` | Max jobs running at once. |
| `SleepAfterCheck` | `200 ms` | Idle poll interval when no job is ready. `Zero`/`MinValue` are normalised to `10 ms`. |

```csharp
var config = new Configuration(maxParallelJobExecute: 5, sleepAfterCheck: TimeSpan.FromMilliseconds(100));
// or: new Configuration(maxParallelJobExecute: 5);   // SleepAfterCheck = 100 ms
// or: Configuration.Default;
```

### Example <a id="example" />

```csharp
using FabulousScheduler.Core.Types;
using FabulousScheduler.Recurring;
using FabulousScheduler.Recurring.Interfaces;
using FabulousScheduler.Recurring.Result;

var config = new Configuration(
    maxParallelJobExecute: 5,
    sleepAfterCheck: TimeSpan.FromMilliseconds(100)
);
RecurringJobManager.SetConfig(config);

// Start the scheduler BEFORE registering jobs
RecurringJobManager.RunScheduler();

// Subscribe to results
RecurringJobManager.JobResultEvent += (ref IRecurringJob job, ref JobResult<JobOk, JobFail> res) =>
{
    var now = DateTime.Now;
    if (res.IsSuccess)
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} OK", now, job.Name, res.JobID);
    else
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} FAIL", now, job.Name, res.JobID);
};

// Register a job that repeats every second after each success
RecurringJobManager.Register(
    action: () =>
    {
        int a = 10, b = 100;
        int c = a + b;
        _ = c;
    },
    sleepDuration: TimeSpan.FromSeconds(1),
    name: "ExampleJob"
);

Thread.Sleep(-1); // keep the process alive
```

---

## Benchmarks <a id="benchmarks" />

_Planned._

### Performance <a id="performance" />

_Planned._
