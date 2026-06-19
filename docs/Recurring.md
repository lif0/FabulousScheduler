# Recurring Scheduler

Recurring jobs run again and again. After a successful run a job sleeps for its `SleepDuration`,
then it's eligible to run again.

### 📖 Contents

- [Default Job Manager](#default)
- [Make my own job manager](#myself)
- [Usage](#usage)
    - [BaseRecurringJob](#baserecurringjob)
    - [BaseRecurringScheduler](#baserecurringshceduler)
    - [Example](#example)
- [Benchmarks](#benchmarks)

---

## Default Job Manager <a id="default" />

`RecurringJobManager` (in `FabulousScheduler.Recurring`) is a static, process-wide facade. Call it
in this order:

```
1. SetConfig(config)   // optional, only before RunScheduler
2. RunScheduler()      // start the background loop
3. JobResultEvent +=   // subscribe to results
4. Register(...)       // add jobs, only after RunScheduler
```

The order is enforced. `Register(...)` before `RunScheduler()` throws
`SchedulerNotRunnableException`, and `SetConfig(...)` after `RunScheduler()` throws
`SetConfigAfterRunSchedulingException`.

### Register overloads

`Register` returns the new job's `Guid`. It takes either a synchronous (`Action`) or an
asynchronous (`Func<Task>`) delegate, with optional `name` and `category`:

```csharp
Guid Register(Action     action,                                 TimeSpan sleepDuration);
Guid Register(Action     action, string name,                    TimeSpan sleepDuration);
Guid Register(Action     action, string name, string category,   TimeSpan sleepDuration);
Guid Register(Func<Task> action,                                 TimeSpan sleepDuration);
Guid Register(Func<Task> action, string name,                    TimeSpan sleepDuration);
Guid Register(Func<Task> action, string name, string category,   TimeSpan sleepDuration);
```

Leave `name`/`category` out and you get `name = "anonymous"`, `category = "internal"`.

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

The parameters are `ref` so the result isn't copied when it's handed to you. Don't throw from this
handler. If you do, the scheduler swallows it (so one bad handler can't take a worker down), and
you'll never see the exception.

### State machine (`JobStateEnum`)

A job moves through these states:

```
Ready ──scheduler picks it──► Waiting ──worker takes it──► Running
  ▲                                                          │
  │                                                          │ run finished
  └────────────── sleep window elapsed ──────────────── Sleeping

  Any state ──Dispose()──► Disposed
```

`SleepDuration` decides the gap between runs:

| `SleepDuration` | What happens after a run |
|-----------------|--------------------------|
| `TimeSpan.Zero` or `TimeSpan.MinValue` | Counted as zero. The job runs again as soon as the next slot is free (the gap is floored to `SleepAfterCheck`, so it can't spin). |
| `TimeSpan.MaxValue` | The job runs once and is then dropped. A one-shot job. |
| anything else | The job runs again after that much time. |

The gap is the same whether a run succeeds or fails: a job that keeps failing retries on its normal
cadence, it does not busy-loop. If you want a real backoff (grow the delay on repeated failures),
build it into your delegate.

### Fail reasons (`JobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | `ExecuteAsync` ran while the job wasn't `Ready`/`Waiting`. |
| `InternalException` | Your delegate threw. The exception is on `JobFail.Exception`. |
| `FailedExecute` | The job returned a `JobFail` on purpose. |
| `Disposed` | The job was disposed before it could run. |

---

## Make my own job manager <a id="myself" />

`RecurringJobManager` is a thin static wrapper over two public base classes. When you want more
(custom job state, your own registry, DI instead of a static singleton), subclass them.

**1. A custom job.** Derive from `BaseRecurringJob` and implement `ActionJob()`:

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

**2. A custom scheduler.** Derive from `BaseRecurringScheduler` and expose registration. The base
gives you `protected bool Register(IRecurringJob)` (and an `IEnumerable<IRecurringJob>` overload),
a matching `Unregister(Guid)`, the background engine, and the `JobResultEvent`:

```csharp
using FabulousScheduler.Recurring.Abstraction;

public sealed class MyRecurringScheduler : BaseRecurringScheduler
{
    public MyRecurringScheduler(Configuration? config) : base(config) { }

    public Guid Add(MyRecurringJob job)
    {
        base.Register(job);   // adds the job to the registry
        return job.ID;
    }

    public bool Remove(Guid id) => base.Unregister(id); // stops scheduling it
}
```

It works like the default manager: `new MyRecurringScheduler(config)`, subscribe to
`JobResultEvent`, `RunScheduler()`, then `Add(job)`.

---

## Usage <a id="usage" />

### BaseRecurringJob <a id="baserecurringjob" />

`public abstract class BaseRecurringJob : IRecurringJob`. A thread-safe state machine that hands the
actual work to the abstract `ActionJob()`.

| Member | Description |
|--------|-------------|
| `ID`, `Name`, `Category` | Identity (see [Core.md](Core.md)). |
| `SleepDuration` | The sleep window after a successful run. `Zero`/`MinValue` become `Zero` in the constructor. |
| `State` | The current `JobStateEnum`. Reading it can move a finished job from `Sleeping` to `Ready` once its sleep is up. |
| `TotalRun` | `ulong`, how many times the job started. |
| `TotalFail` | `ulong`, how many runs failed. |
| `LastExecute` / `LastSuccessExecute` | Timestamps (nullable). |
| `Task<JobResult<JobOk,JobFail>> ExecuteAsync()` | Runs the job once and updates the state and counters. |
| `protected abstract Task<JobResult<JobOk,JobFail>> ActionJob()` | Your work. Return `JobOk` or `JobFail`. |
| `Dispose()` / `DisposeAsync()` | Marks the job disposed. `DisposeAsync` just calls `Dispose`. |

Constructor:

```csharp
protected BaseRecurringJob(string name, string category, TimeSpan sleepDuration, bool isAsyncAction);
```

### BaseRecurringScheduler <a id="baserecurringshceduler" />

`public abstract class BaseRecurringScheduler : IRecurringJobScheduler`.

| Member | Description |
|--------|-------------|
| `event JobResultEventHandler JobResultEvent` | Fires after each run with `(ref IRecurringJob, ref JobResult<JobOk,JobFail>)`. |
| `void RunScheduler()` | Starts the engine. A second call does nothing. |
| `int CurrentRunnableJobCount()` | How many jobs are running right now. |
| `protected bool Register(IRecurringJob job)` | Adds one job. Returns `false` if its `ID` is already there. |
| `protected int Register(IEnumerable<IRecurringJob> jobs)` | Adds many. Returns how many were added. |
| `protected bool Unregister(Guid id)` | Stops scheduling a job. Any run already in flight finishes first. |
| `void Dispose()` | Cancels the engine and waits for it to stop. |

How it runs: a single producer keeps the registered jobs in a min-heap ordered by their next run
time, so finding the next one to run is cheap even with a lot of jobs. The producer sleeps until
that next job is due (it doesn't poll), then pushes the due jobs onto a channel. A fixed pool of
`MaxParallelJobExecute` workers reads the channel and runs them, which is what caps the parallelism.
When a job finishes, it's scheduled for its next run, or dropped from the registry if it was a
one-shot (`SleepDuration == MaxValue`) or you unregistered it.

### Configuration

`FabulousScheduler.Recurring.Configuration`:

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `Environment.ProcessorCount * 10` | Number of workers, so the most jobs running at once. |
| `SleepAfterCheck` | `200 ms` | The smallest gap between a job's runs (the floor for short sleep durations). `Zero`/`MinValue` become `10 ms`. |

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

// Start the scheduler before registering jobs
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

// A job that repeats one second after each success
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

The numbers for the recurring scheduler (how it picks the next job, and end-to-end throughput) are
on the [Benchmarks](Benchmarks.md) page.
