# Queue-based Scheduler

Queue jobs run once per enqueue, in FIFO order. The scheduler pulls the next job from an `IQueue`,
runs it, reports the result, and moves on. To run a job again, put it back in the queue.

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

---

## Default Job Manager <a id="default" />

`QueueJobManager` (in `FabulousScheduler.Queue`) is a static, process-wide facade. Unlike the
recurring one, it needs an `IQueue`. Call it in this order:

```
1. SetConfig(config, queue)   // optional config, but the queue is required; before RunScheduler
2. RunScheduler()             // start the background loop
3. JobResultEvent +=          // subscribe to results
4. Register(...)              // enqueue jobs, only after RunScheduler
```

Same two rules as the recurring manager. `Register(...)` before `RunScheduler()` throws
`SchedulerNotRunnableException`, and `SetConfig(...)` after `RunScheduler()` throws
`SetConfigAfterRunSchedulingException`.

### Register overloads

`Register` builds a job, enqueues it right away, and returns its `Guid`:

```csharp
Guid Register(Action     action);
Guid Register(Action     action, string name);
Guid Register(Action     action, string name, string category);
Guid Register(Func<Task> action);
Guid Register(Func<Task> action, string name);
Guid Register(Func<Task> action, string name, string category);
```

Leave the optional parts out and you get `name = "anonymous"`, `category = "internal"`.

Jobs registered this way run once. The manager doesn't expose the `Attempts` budget, so if you want
the built-in retry counter you build the jobs yourself. See [Retry / Attempts](#attempts).

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

Don't throw from this handler. The scheduler swallows it so a bad handler can't kill a worker, and
the exception is then lost.

### State machine (`QueueJobStateEnum`)

```
Waiting ──worker takes it──► Running ──run finished──► Completed
   ▲                                                      │
   └──────────────── ResetState() ────────────────────────┘
              (then Enqueue again to re-run)
```

A `Completed` job won't run again by itself. To re-run it, call `ResetState()` (`Completed →
Waiting`) and put it back in the queue.

### Fail reasons (`QueueJobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | `ExecuteAsync` ran while the job wasn't `Waiting`. |
| `InternalException` | Your delegate threw. The exception is on `JobFail.Exception`. |
| `FailedExecute` | The job returned a `JobFail` on purpose. |
| `Disposed` | The job was disposed before it could run. |

---

## Make my own job manager <a id="myself" />

`QueueJobManager` wraps two public base classes plus an `IQueue`. Subclass them when you want
custom job state, your own queue (a database-backed one, say), or DI instead of a static singleton.

**1. A custom job.** Derive from `BaseQueueJob` and implement `ActionJob()`. The constructor also
takes the optional `Attempts` budget:

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

**2. A custom scheduler.** Derive from `BaseQueueScheduler`. The workers pull from the
`protected IQueue Queue`, so registering a job is just an enqueue:

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

**3. A custom queue.** Implement `IQueue` to store the queue wherever you want (Redis, PostgreSQL,
and so on). The contract is small:

```csharp
public interface IQueue
{
    int Count { get; }
    void Enqueue(IQueueJob job);
    ValueTask<IQueueJob> NextAsync(CancellationToken cancellationToken = default);
}
```

`NextAsync` returns the next job. When the queue is empty it waits (asynchronously) until one is
enqueued, and it should give up if the token is cancelled, so the scheduler can stop cleanly on
shutdown.

---

## Usage <a id="usage" />

### BaseQueueJob <a id="basequeuejob" />

`public abstract class BaseQueueJob : IQueueJob`. Thread-safe state, an optional `Attempts` budget,
work handed to the abstract `ActionJob()`.

| Member | Description |
|--------|-------------|
| `ID`, `Name`, `Category` | Identity (see [Core.md](Core.md)). |
| `Attempts` | `byte?`, attempts left. Decremented on each run when set; `null` means it isn't tracked. |
| `State` | The current `QueueJobStateEnum`. |
| `TotalRun` | `uint`, how many times the job started. |
| `LastExecute` / `LastSuccessExecute` | Timestamps (nullable). |
| `Task<JobResult<JobOk,JobFail>> ExecuteAsync()` | Runs the job once. |
| `void ResetState()` | `Completed → Waiting`, so the job can be enqueued again. |
| `protected abstract Task<JobResult<JobOk,JobFail>> ActionJob()` | Your work. |
| `Dispose()` / `DisposeAsync()` | Marks the job disposed. |

Constructor:

```csharp
protected BaseQueueJob(string name, string category, bool isAsyncAction, byte? attempts);
```

### BaseQueueScheduler <a id="basequeueshceduler" />

`public class BaseQueueScheduler : IQueueJobScheduler`. The constructor is `protected`, so use it
through a subclass.

| Member | Description |
|--------|-------------|
| `event JobResultEventHandler JobResultEvent` | Fires after each run with `(ref IQueueJob, ref JobResult<JobOk,JobFail>)`. |
| `void RunScheduler()` | Starts the workers. A second call does nothing. |
| `protected readonly IQueue Queue` | The backing queue. `Enqueue` to add work. |
| `void Dispose()` | Cancels the workers and waits for them to stop. |

It runs a fixed pool of `MaxParallelJobExecute` workers. Each worker takes one job from
`Queue.NextAsync(token)`, runs it, raises `JobResultEvent`, and loops. The pool size is what caps
the parallelism, there's no separate Task per job.

### InMemoryQueue <a id="inmemoryqueue" />

`FabulousScheduler.Queue.Queues.InMemoryQueue` is the built-in `IQueue`. It's backed by an unbounded
`Channel`. Enqueue a job and a waiting worker picks it up; if no worker is waiting, it sits in the
buffer until one asks for it.

| Member | Description |
|--------|-------------|
| `InMemoryQueue(int? capacity = null)` | Optional capacity hint. It's advisory, not a hard limit. |
| `int Count` | Number of buffered jobs. Workers that are waiting don't count, and neither do jobs already running. |
| `void Enqueue(IQueueJob job)` | Add one job. |
| `void Enqueue(IEnumerable<IQueueJob> jobs)` | Add many. This one is on `InMemoryQueue` only, not on `IQueue`. |
| `ValueTask<IQueueJob> NextAsync(CancellationToken cancellationToken = default)` | The next job. When a job is already waiting this returns without allocating; otherwise it waits for one. |

### Retry / Attempts <a id="attempts" />

`BaseQueueScheduler` doesn't retry on its own. Retrying is up to you: when a job fails and still has
budget, reset it and enqueue it again.

The `Attempts` budget is a `byte?` that the base decrements on each run. `QueueJobManager.Register`
doesn't expose it, so it's only there when you build the jobs yourself (a custom job plus a custom
scheduler, or your own `IQueue`):

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

On the default `QueueJobManager`, keep a reference to the `queue` you passed to `SetConfig` and
re-enqueue from the callback with your own counter (the built-in `Attempts` is `null` there).

### Configuration

`FabulousScheduler.Queue.Configuration`:

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `Environment.ProcessorCount * 2` | Number of workers, so the most jobs running at once. |

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

// Start the scheduler before registering jobs
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

Numbers for the queue scheduler (the cost of taking a job, and end-to-end throughput) are on the
[Benchmarks](Benchmarks.md) page.
