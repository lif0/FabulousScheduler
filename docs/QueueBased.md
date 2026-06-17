# Queue-based Scheduler

Queue jobs run **once per enqueue**, in FIFO order. The scheduler pulls the next job from
an `IQueue`, runs it, reports the result, and moves on. Re-running a job means putting it
back into the queue.

### 📖 Contents

- [Default Job Manager](#default)
- [Make my own job manager](#myself)
- [Usage](#usage)
  - [BaseQueueJob](#basequeuejob)
  - [BaseQueueScheduler](#basequeueshceduler)
  - [InMemoryQueue](#inmemoryqueue)
  - [Retry / Attempts](#attempts)
  - [Example](#example)
- [Benchmarks](#benchmarks)
  - [Performance](#performance)

---

## Default Job Manager <a id="default" />

`QueueJobManager` (in `FabulousScheduler.Queue`) is a static, process-wide facade. Unlike
the recurring manager, it needs an `IQueue` instance. The required call order is:

```
1. SetConfig(config, queue)   // OPTIONAL config, but queue is required; before RunScheduler
2. RunScheduler()             // start the background loop
3. JobResultEvent +=          // subscribe to results
4. Register(...)              // enqueue jobs (only AFTER RunScheduler)
```

> ⚠️ Same enforced contracts as the recurring manager:
> - `Register(...)` before `RunScheduler()` throws `SchedulerNotRunnableException`;
> - `SetConfig(...)` after `RunScheduler()` throws `SetConfigAfterRunSchedulingException`.

### Register overloads

`Register` creates a job, enqueues it immediately, and returns its `Guid`:

```csharp
Guid Register(Action     action);
Guid Register(Action     action, string name);
Guid Register(Action     action, string name, string category);
Guid Register(Func<Task> action);
Guid Register(Func<Task> action, string name);
Guid Register(Func<Task> action, string name, string category);
```

Defaults when omitted: `name = "anonimouse"`, `category = "internal"`.

> ℹ️ Jobs registered through `QueueJobManager` are **single-run** — the manager does not
> expose the `Attempts` budget. To use the built-in `Attempts` counter you build the jobs
> yourself; see [Retry / Attempts](#attempts).

### The result callback

```csharp
QueueJobManager.JobResultEvent +=
    (ref IQueueJob job, ref JobResult<JobOk, JobFail> res) =>
    {
        if (res.IsSuccess)
            Console.WriteLine("{0} ok", job.Name);
        else
            Console.WriteLine("{0} failed: {1}", job.Name, res.GetFail()!.Message);
    };
```

### State machine (`QueueJobStateEnum`)

```
Waiting ──dequeued──► Running ──run finished──► Completed
   ▲                                                │
   └──────────── ResetState() ──────────────────────┘
          (then Enqueue again to re-run)
```

A `Completed` job will not run again on its own. To re-run it, call `ResetState()`
(`Completed → Waiting`) and put it back into the queue.

### Fail reasons (`QueueJobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | `ExecuteAsync` was called while the job was not `Waiting`. |
| `InternalException` | The user delegate threw — the exception is in `JobFail.Exception`. |
| `FailedExecute` | The job explicitly returned a `JobFail`. |
| `Disposed` | The job was disposed before execution. |

---

## Make my own job manager <a id="myself" />

`QueueJobManager` wraps two public base classes plus an `IQueue`. Subclass them when you
need custom job state, your own queue (e.g. a database-backed queue), or DI instead of a
static singleton.

**1. A custom job** — derive from `BaseQueueJob` and implement `ActionJob()`. The
constructor also takes the optional `Attempts` budget:

```csharp
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Core.Types;

public sealed class MyQueueJob : BaseQueueJob
{
    public MyQueueJob(string name, string category, byte? attempts)
        : base(name, category, isAsyncAction: true, attempts) { }

    protected override async Task<JobResult<JobOk, JobFail>> ActionJob()
    {
        await Task.Delay(10);
        return new JobOk(ID, Name);            // success
        // return new JobFail(QueueJobFailEnum.FailedExecute, ID, "reason"); // failure
    }
}
```

**2. A custom scheduler** — derive from `BaseQueueScheduler`. The base loop pulls jobs
from the `protected IQueue Queue`, so registration is just an enqueue:

```csharp
using FabulousScheduler.Queue.Abstraction;
using FabulousScheduler.Queue.Interfaces;

public sealed class MyQueueScheduler : BaseQueueScheduler
{
    public MyQueueScheduler(Configuration? config, IQueue queue) : base(config, queue) { }

    public Guid Add(MyQueueJob job)
    {
        base.Queue.Enqueue(job);
        return job.ID;
    }
}
```

**3. A custom queue** — implement `IQueue` to back the queue with anything you like
(Redis, PostgreSQL, …). The contract is small:

```csharp
public interface IQueue
{
    int Count { get; }
    void Enqueue(IQueueJob job);
    Task<IQueueJob> NextAsync();   // returns the next job, or completes later when one arrives
}
```

`NextAsync()` is expected to **wait** (asynchronously) when the queue is empty and complete
once a job is enqueued.

---

## Usage <a id="usage" />

### BaseQueueJob <a id="basequeuejob" />

`public abstract class BaseQueueJob : IQueueJob`. Thread-safe state, optional `Attempts`
budget, work delegated to the abstract `ActionJob()`.

| Member | Description |
|--------|-------------|
| `ID`, `Name`, `Category` | Identity (see [Core.md](Core.md)). |
| `Attempts` | `byte?` — remaining attempts. Decremented on each run when set; `null` means "not tracked". |
| `State` | Current `QueueJobStateEnum`. |
| `TotalRun` | `uint` — number of times the job started. |
| `LastExecute` / `LastSuccessExecute` | Timestamps (nullable). |
| `Task<JobResult<JobOk,JobFail>> ExecuteAsync()` | Runs the job once. |
| `void ResetState()` | `Completed → Waiting`, so the job can be re-enqueued. |
| `protected abstract Task<JobResult<JobOk,JobFail>> ActionJob()` | Your work. |
| `Dispose()` / `DisposeAsync()` | Marks the job disposed. |

Constructor:

```csharp
protected BaseQueueJob(string name, string category, bool isAsyncAction, byte? attempts);
```

### BaseQueueScheduler <a id="basequeueshceduler" />

`public class BaseQueueScheduler : IQueueJobScheduler` (constructor is `protected`, so use
it via a subclass). Runs one `LongRunning` background loop that awaits `Queue.NextAsync()`.

| Member | Description |
|--------|-------------|
| `event JobResultEventHandler JobResultEvent` | Fired after each job run with `(ref IQueueJob, ref JobResult<JobOk,JobFail>)`. |
| `void RunScheduler()` | Starts the loop. Idempotent. |
| `protected readonly IQueue Queue` | The backing queue — `Enqueue` to add work. |
| `void Dispose()` | Requests cancellation and releases resources. |

A `SemaphoreSlim` caps concurrent runs at `Configuration.MaxParallelJobExecute`.

### InMemoryQueue <a id="inmemoryqueue" />

`FabulousScheduler.Queue.Queues.InMemoryQueue` is the built-in `IQueue`. It keeps a FIFO
of pending jobs and a FIFO of waiting `NextAsync()` requests; enqueuing a job either lands
in the backlog or directly completes a waiting request.

| Member | Description |
|--------|-------------|
| `InMemoryQueue(int? capacity = null)` | Optional capacity hint for the waiting-requests queue. |
| `int Count` | Number of **backlogged** jobs (not counting in-flight ones). |
| `void Enqueue(IQueueJob job)` | Add a single job. |
| `void Enqueue(IEnumerable<IQueueJob> jobs)` | Add many jobs (extra method, not on `IQueue`). |
| `Task<IQueueJob> NextAsync()` | Next job, or a task that completes when one is enqueued. |

### Retry / Attempts <a id="attempts" />

`BaseQueueScheduler` **does not retry automatically.** Retry is the caller's
responsibility: when a job fails and still has budget, reset it and enqueue it again.

The `Attempts` budget on a job is a `byte?` that the base decrements on each run. Because
`QueueJobManager.Register` does not expose it, the budget is only available when you build
the jobs yourself (custom job + custom scheduler, or by holding your own `IQueue`):

```csharp
var queue = new InMemoryQueue();
var scheduler = new MyQueueScheduler(Configuration.Default, queue);

scheduler.JobResultEvent += (ref IQueueJob sender, ref JobResult<JobOk, JobFail> res) =>
{
    if (res.IsFail && sender.Attempts is > 0)
    {
        sender.ResetState();   // Completed -> Waiting
        queue.Enqueue(sender); // try again
    }
};

scheduler.RunScheduler();
scheduler.Add(new MyQueueJob("retryable", "demo", attempts: 3));
```

If you are using the default `QueueJobManager`, keep a reference to the `queue` you passed
to `SetConfig` and re-enqueue from the callback using your own retry counter (the built-in
`Attempts` will be `null`).

### Configuration

`FabulousScheduler.Queue.Configuration`:

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `Environment.ProcessorCount * 2` | Max jobs running at once. |

```csharp
var config = new Configuration(maxParallelJobExecute: 5);
// or: Configuration.Default;
```

### Example <a id="example" />

```csharp
using FabulousScheduler.Queue;
using FabulousScheduler.Queue.Interfaces;
using FabulousScheduler.Queue.Queues;
using FabulousScheduler.Queue.Result;
using FabulousScheduler.Core.Types;

var queue = new InMemoryQueue(1);

var config = new Configuration(maxParallelJobExecute: 5);
QueueJobManager.SetConfig(config, queue);

// Start the scheduler BEFORE registering jobs
QueueJobManager.RunScheduler();

// Subscribe to results
QueueJobManager.JobResultEvent += (ref IQueueJob job, ref JobResult<JobOk, JobFail> res) =>
{
    var now = DateTime.Now;
    if (res.IsSuccess)
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} OK", now, job.Name, res.JobID);
    else
        Console.WriteLine("[{0:hh:mm:ss}] {1} {2} FAIL", now, job.Name, res.JobID);
};

// Enqueue five one-shot jobs
for (var i = 0; i < 5; i++)
{
    QueueJobManager.Register(
        action: () =>
        {
            int a = 10, b = 100;
            int c = a + b;
            _ = c;
        },
        name: $"ExampleJob_{i}"
    );
}

Thread.Sleep(-1); // keep the process alive
```

---

## Benchmarks <a id="benchmarks" />

_Planned._

### Performance <a id="performance" />

_Planned._
