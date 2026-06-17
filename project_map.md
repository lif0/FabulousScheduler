# FabulousScheduler — Project Map

High-performance recurring & queue-based job scheduler for .NET 6/7/8. Published on NuGet.

---

## Solution layout

```
FabulousScheduler/
├── src/
│   ├── FabulousScheduler.sln
│   ├── FabulousScheduler.Core/          # NuGet: FabulousScheduler.Core
│   ├── FabulousScheduler.Recurring/     # NuGet: FabulousScheduler.Cron
│   ├── FabulousScheduler.Queue/         # NuGet: FabulousScheduler.Queue
│   ├── FabulousScheduler/               # NuGet: FabulousScheduler  (meta-package)
│   ├── FabulousScheduler.UnitTests/     # xUnit tests
│   ├── Benchmark/Common/                # BenchmarkDotNet benchmarks
│   └── Samples/
│       ├── FabulousScheduler.RecurringSample/
│       └── FabulousScheduler.QueueSample/
├── docs/
│   ├── Core.md
│   ├── Recurring.md
│   └── QueueBased.md
└── .github/workflows/                   # CI: build, tests, NuGet publish
```

---

## Package dependency graph

```
FabulousScheduler.Core
       ↑           ↑
FabulousScheduler  FabulousScheduler
   .Recurring          .Queue
       ↑           ↑
    FabulousScheduler   ← meta-package (references both)
```

---

## Layer-by-layer description

### 1. Core (`FabulousScheduler.Core`)

Shared primitives used by both subsystems.

| File | Purpose |
|------|---------|
| `Interfaces/IJob.cs` | Base job identity: `ID`, `Name`, `LastExecute`, `LastSuccessExecute` |
| `Interfaces/IJobScheduler.cs` | Scheduler entry-point: `RunScheduler()` |
| `Interfaces/Result/IJobOk.cs` | Successful result marker — carries `ID` |
| `Interfaces/Result/IJobFail.cs` | Failed result marker — carries `ID` |
| `Types/JobResult<TOk, TFail>.cs` | Discriminated union result. Methods: `Match`, `MatchAsync`, `Do`, `GetFail`, `JobID`, `IsFail/IsSuccess` |

---

### 2. Recurring subsystem (`FabulousScheduler.Recurring`)

Jobs that repeat on a fixed interval after each successful run.

#### Interfaces

| Interface | Extends | Key members |
|-----------|---------|-------------|
| `IRecurringJob` | `IJob` | `Category`, `State`, `SleepDuration`, `TotalRun`, `TotalFail`, `ExecuteAsync()`, `ResetState()` (internal) |
| `IRecurringJobScheduler` | `IJobScheduler` | `event JobResultEventHandler JobResultEvent` |

#### State machine (`JobStateEnum`)

```
Ready ──RunScheduler──► Waiting ──dequeue──► Running
  ▲                                              │
  │          SleepDuration==0 or expired         │ success
  └─────────────────────────────────────────────►Sleeping
  ◄──── SleepDuration expired ──────────────────┘

  Running → Ready  (if fail — because fail doesn't start sleep)
  Any     → Disposed (on Dispose())
```

#### Fail reasons (`JobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | Job not in `Ready` or `Waiting` when Execute called |
| `InternalException` | Unhandled exception inside `ActionJob` |
| `FailedExecute` | Job explicitly returned `JobFail` |
| `Disposed` | Job was disposed before execution |

#### Abstraction layer

| Class | Role |
|-------|------|
| `BaseRecurringJob` | Implements `IRecurringJob`. Thread-safe state machine. Tracks `TotalRun`, `TotalFail`, timestamps. Delegates work to abstract `ActionJob()`. |
| `BaseRecurringScheduler` | Main loop on a dedicated `LongRunning` task. Uses `SemaphoreSlim` for parallelism cap. `ConcurrentDictionary` for in-progress jobs. `ConcurrentQueue` for ready-to-run queue. Polling loop with `Thread.Sleep(SleepAfterCheck)` when no jobs are ready. |

#### Internal implementations (inside `FabulousScheduler` meta-package)

| Class | Role |
|-------|------|
| `RecurringJob` | Seals `BaseRecurringJob`. Holds `Action` or `Func<Task>` delegate. |
| `RecurringScheduler` | Seals `BaseRecurringScheduler`. Exposes `Register(Action/Func<Task>, name, category, sleepDuration)`. |

#### Public API

```csharp
// Static facade — singleton scheduler
RecurringJobManager.SetConfig(config);          // before RunScheduler
RecurringJobManager.RunScheduler();
RecurringJobManager.JobResultEvent += handler;
Guid id = RecurringJobManager.Register(action, name, category, sleepDuration);
```

#### Configuration (`FabulousScheduler.Recurring.Configuration`)

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `ProcessorCount * 10` | SemaphoreSlim initial count |
| `SleepAfterCheck` | `200 ms` | Polling interval when no jobs are ready |

---

### 3. Queue subsystem (`FabulousScheduler.Queue`)

Jobs run once (or N times via Attempts) in the order they are enqueued.

#### Interfaces

| Interface | Extends | Key members |
|-----------|---------|-------------|
| `IQueueJob` | `IJob` | `TotalRun`, `Attempts`, `State`, `ExecuteAsync()`, `ResetState()` |
| `IQueueJobScheduler` | `IJobScheduler` | `event JobResultEventHandler JobResultEvent` |
| `IQueue` | — | `Count`, `Enqueue(job)`, `Enqueue(jobs)`, `NextAsync()` (async blocking dequeue) |

#### State machine (`QueueJobStateEnum`)

```
Waiting ──dequeue──► Running ──► Completed
```

After `Completed`, a job can be re-queued by calling `ResetState()` + `IQueue.Enqueue()` (used in retry/Attempts pattern).

#### Fail reasons (`QueueJobFailEnum`)

| Value | When |
|-------|------|
| `IncorrectState` | Job not in `Waiting` when Execute called |
| `InternalException` | Unhandled exception inside `ActionJob` |
| `FailedExecute` | Job explicitly returned `JobFail` |
| `Disposed` | Job was disposed |

#### Queue implementations

| Class | Storage | Notes |
|-------|---------|-------|
| `InMemoryQueue` | `Queue<IQueueJob>` + `Queue<TaskCompletionSource<IQueueJob>>` | Lock-based. `NextAsync` returns a TCS task when queue is empty; completing when a job is enqueued. Optional capacity hint. |

#### Abstraction layer

| Class | Role |
|-------|------|
| `BaseQueueJob` | Implements `IQueueJob`. Thread-safe state. Supports optional `Attempts` counter (decremented on each run). Abstract `ActionJob()`. |
| `BaseQueueScheduler` | `LongRunning` main loop. Calls `Queue.NextAsync()` (blocking). Uses `SemaphoreSlim`. `ConcurrentDictionary` for in-progress tasks. |

#### Retry pattern (Attempts)

`BaseQueueScheduler` does **not** retry automatically. Retry is the caller's responsibility: subscribe to `JobResultEvent`, check `IsFail && sender.Attempts > 0`, then call `sender.ResetState(); Queue.Enqueue(sender)`. Demonstrated in `TestQueueSchedulerWithAttempts`.

#### Internal implementations

| Class | Role |
|-------|------|
| `QueueJob` | Seals `BaseQueueJob`. Holds `Action` or `Func<Task>`. |
| `QueueScheduler` | Extends `BaseQueueScheduler`. `Register` creates `QueueJob` and enqueues immediately. |

#### Public API

```csharp
var queue = new InMemoryQueue();
QueueJobManager.SetConfig(config, queue);
QueueJobManager.RunScheduler();
QueueJobManager.JobResultEvent += handler;
Guid id = QueueJobManager.Register(action, name, category);
```

#### Configuration (`FabulousScheduler.Queue.Configuration`)

| Property | Default | Meaning |
|----------|---------|---------|
| `MaxParallelJobExecute` | `ProcessorCount * 2` | SemaphoreSlim initial count |

---

### 4. Meta-package (`FabulousScheduler`)

Umbrella NuGet that references both `FabulousScheduler.Recurring` and `FabulousScheduler.Queue`. Contains:

- `RecurringJobManager` (static facade)
- `QueueJobManager` (static facade)
- `Exception/SchedulerNotRunnableException` — thrown if `Register` is called before `RunScheduler`
- `Exception/SetConfigAfterRunSchedulingException` — thrown if `SetConfig` is called after `RunScheduler`

---

## Concurrency model (shared pattern)

Both schedulers use the same structure:

```
Main loop (LongRunning Task)
    │
    ├─ Acquire SemaphoreSlim slot
    ├─ Dequeue next job
    └─ Task.Factory.StartNew (DenyChildAttach)
           │
           ├─ job.ExecuteAsync()
           ├─ Remove from _inProgress
           ├─ Fire JobResultEvent
           └─ Release SemaphoreSlim slot
```

Key synchronization primitives:

| Primitive | Used for |
|-----------|---------|
| `lock` | State transitions inside jobs; scheduler init; job registration dict |
| `SemaphoreSlim` | Parallel job limit |
| `ConcurrentDictionary` | In-progress job tracking |
| `ConcurrentQueue` (Recurring) | Ready-to-run job queue |
| `TaskCompletionSource` (InMemoryQueue) | Async blocking dequeue |
| `Interlocked` | `TotalRun`, `TotalFail` counters; test counters |
| `CancellationTokenSource` | Scheduler shutdown |

---

## Result type design

`JobResult<TOk, TFail>` is a discriminated union (no exceptions for control flow):

```csharp
// Implicit conversion from ok/fail value:
JobResult<JobOk, JobFail> result = new JobOk(id);
JobResult<JobOk, JobFail> result = new JobFail(reason, id, msg);

// Consuming:
result.Do(ok => ..., fail => ...);
result.Match(ok => ..., fail => ...);
await result.MatchAsync(ok => ..., fail => ...);
(TResult, TFailResult) = result.Match<TResult, TFailResult>(f);
var fail = result.GetFail();   // null if success
```

`Recurring.Result.JobFail` is a plain class.
`Queue.Result.JobFail` extends `Exception` (note: design inconsistency vs Recurring).

---

## Tests (`FabulousScheduler.UnitTests`)

Framework: **xUnit**.

| Test class | What is tested |
|------------|----------------|
| `Recurring/JobTests` | Single-job state machine: Disposed, IncorrectState, FailedExecute, InternalException, SleepDuration transitions (Zero / MinValue / nonzero) |
| `Recurring/JobManagerTests` | Scheduler integration: FailOne, SuccessOne, 1k/5k/50k throughput, sleep period in/out |
| `Recurring/DefaultJobManagerTests` | `RecurringJobManager` static facade via `RegisterJob` |
| `Queue/JobManagerTests` | FailOne, FailExp, SuccessOne, DuplicateJob (IncorrectState on second run), AttemptsFailNextOk, 1k/5k throughput |
| `Queue/Common` | Test stubs: `QueueRandomJob`, `QueueJobFailResult`, `QueueJobOkResult`, `QueueJobFailExceptionResult`, `QueueJobAttemptsFailNextOk`, `TestQueueScheduler`, `TestQueueSchedulerWithAttempts` |
| `Recurring/Common` | Test stubs: `RecurringJobRandomResult`, `RecurringJobOkResult`, `RecurringJobFailedExecuteResult`, `RecurringJobInternalExceptionResult`, `TestRecurringScheduler`, `Helper` |

---

## Benchmark (`Benchmark/Common`)

BenchmarkDotNet, target `.NET 7`. Benchmarks sync vs async delegate invocation overhead:
- `MethodCoverTaskEmptyAction` — empty action cost
- `MethodCoverTask` — SHA256 computation (1k/10k bytes) to separate framework overhead from work
- `CoverSyncAndAsyncMethodTask` — the benchmark subject (mirrors `BaseRecurringJob` / `BaseQueueJob` dispatch pattern)

---

## CI/CD (`.github/workflows`)

| Workflow | Trigger | Action |
|----------|---------|--------|
| `github-actions-build.yml` | push to main | `dotnet build` |
| `github-actions-tests.yml` | push | `dotnet test` |
| `github-actions-push-nuget.yml` | push | Pack & publish to NuGet |

---

## Key design decisions / notable facts

- Both schedulers use **polling** (Recurring) or **async blocking** (Queue) — no timers.
- `SleepDuration` in Recurring tracks from **last successful** execute, not from last any execute.
- Recurring fails do **not** start a sleep period — the job goes back to `Ready` immediately.
- Queue jobs are **single-use** by default; retry requires explicit re-enqueue after checking `Attempts`.
- `JobResultEvent` delegates use `ref` parameters to avoid allocation when forwarding.
- `RecurringJobManager` and `QueueJobManager` are process-wide singletons (static state).
- Default job name when not specified: `"anonimouse"` (typo in source — intentional or not).
- `DisposeAsync` in `BaseRecurringJob` delegates synchronously to `Dispose` (marked with TODO).
- `Dispose` in `BaseQueueJob` marked with TODO — potential bug if dispose races with execute.
