# Benchmarks

Small benchmarks for FabulousScheduler, made with [BenchmarkDotNet](https://benchmarkdotnet.org/).
The benchmark project is in [`src/Benchmark/Common`](../src/Benchmark/Common).

### 📖 Contents

- [How to run](#how-to-run)
- [What got better by version](#by-version)
- [JobResult&lt;TOk, TFail&gt;](#jobresult)
  - [Results](#jobresult-results)
  - [Takeaways](#jobresult-takeaways)
- [Job dispatch](#dispatch) (changed in v5.1.0)
  - [Results](#dispatch-results)
  - [Takeaways](#dispatch-takeaways)
- [Picking the next job](#next-job) (changed in v5.1.0)
  - [Results](#next-job-results)
  - [Takeaways](#next-job-takeaways)
- [Work queue (take)](#work-queue) (changed in v5.1.0)
  - [Results](#work-queue-results)
  - [Takeaways](#work-queue-takeaways)
- [Scheduler throughput](#throughput)
  - [Results](#throughput-results)
  - [Takeaways](#throughput-takeaways)

---

## How to run <a id="how-to-run" />

Benchmarks must run in **Release**. Use the CLI filter to pick a suite:

```shell
# all benchmarks
dotnet run -c Release --project src/Benchmark/Common -- --filter *

# only the JobResult suite
dotnet run -c Release --project src/Benchmark/Common -- --filter *JobResult*

# list everything without running
dotnet run -c Release --project src/Benchmark/Common -- --list flat
```

---

## What got better by version <a id="by-version" />

A short list of what changed, so you can see where things got faster.
Numbers come from the suites below. They depend on the machine (here: Arm64, .NET 8), so read them
as "how much better", not as exact values.

### v5.1.0

**1. Starting a job is cheaper.**
Before, the scheduler made a new `Task` for every single job. Now it uses a small pool of workers,
and each worker takes jobs one by one. So a `Task` is made once per worker, not once per job.

| Per job | v5.0.1 | v5.1.0 | Better by |
|---------|-------:|-------:|----------:|
| Memory  |  216 B |   72 B |   −67 %   |
| Time    | ~445 ns| ~188 ns|   −58 %   |

Details: [Job dispatch](#dispatch).

**2. Finding the next job to run is much cheaper.**
Before, on every tick the recurring scheduler looked at **all** jobs to find the ready ones.
Now it keeps jobs in a min-heap (sorted by their next run time), so the next job is just the top
of the heap.

| Find next job | v5.0.1 (look at all) | v5.1.0 (heap) |
|---------------|---------------------:|--------------:|
| 1 000 jobs    |              ~69 µs  |       ~41 ns  |
| 50 000 jobs   |            ~3.65 ms  |       ~62 ns  |

The scheduler now also **sleeps until the next job is due**, instead of waking up again and again.
So when it is idle, it does not waste CPU.

Details: [Picking the next job](#next-job).

**3. Taking a job from the queue is cheaper.**
Both work queues now use a `Channel<T>`. When a job is already waiting, taking it returns a
`ValueTask` that is already done, so it does not create a new object.

| Take one job (job ready) | v5.0.1 | v5.1.0 |
|--------------------------|-------:|-------:|
| Memory                   |   72 B |    0 B |
| Time                     | ~32 ns | ~26 ns |

Note: `IQueue.NextAsync()` changed — it now returns `ValueTask<IQueueJob>` and takes a
`CancellationToken`. This is a **breaking change** if you wrote your own `IQueue`.

Details: [Work queue (take)](#work-queue).

**4. Small clean-up.**
`ExecuteAsync` no longer blocks on `ActionJob().Result`; it uses `await ActionJob()`.
Same speed, but safer and simpler.

### v5.0.1

First version we measured. It is the "before" baseline for the numbers above.

---

## JobResult&lt;TOk, TFail&gt; <a id="jobresult" />

[`JobResult<TOk, TFail>`](Core.md#jobresult) is the value every job returns. It is a `class`, so
every result is a new object on the heap. This suite
([`JobResultBenchmark.cs`](../src/Benchmark/Common/JobResultBenchmark.cs)) measures that memory and
the cost of reading the result (`Do`, `Match`, `JobID`). The success/fail values and the delegates
are made once in `[GlobalSetup]`, so the numbers show `JobResult` itself, not the work inside.

### Results <a id="jobresult-results" />

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Method         | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|----------------|----------:|----------:|----------:|------:|-------:|----------:|------------:|
| Create_Success | 4.3094 ns | 0.0909 ns | 0.1010 ns |  1.00 | 0.0191 |      40 B |        1.00 |
| Create_Fail    | 4.2007 ns | 0.1187 ns | 0.0991 ns |  0.98 | 0.0191 |      40 B |        1.00 |
| Do_Success     | 0.1781 ns | 0.0171 ns | 0.0134 ns |  0.04 |      - |         - |        0.00 |
| Do_Fail        | 0.6017 ns | 0.0200 ns | 0.0156 ns |  0.14 |      - |         - |        0.00 |
| Match_Success  | 0.3494 ns | 0.0261 ns | 0.0310 ns |  0.08 |      - |         - |        0.00 |
| Match_Fail     | 0.7756 ns | 0.0070 ns | 0.0054 ns |  0.18 |      - |         - |        0.00 |
| JobID_Success  | 0.4113 ns | 0.0037 ns | 0.0031 ns |  0.10 |      - |         - |        0.00 |
| JobID_Fail     | 0.3608 ns | 0.0067 ns | 0.0063 ns |  0.08 |      - |         - |        0.00 |

> Numbers depend on the machine (here: Arm64, .NET 8). Read them as relative, not exact.

### Takeaways <a id="jobresult-takeaways" />

- **Every result uses ~40 B of memory**, because `JobResult<TOk, TFail>` is a class. With many jobs
  per second, this is the main memory cost of the result path.
- **Reading the result is almost free**: `Do`, `Match<T>` and `JobID` take less than a nanosecond
  and use no memory. The success/fail check is not a problem.
- Making `JobResult` a `struct` (see the [roadmap](../README.md#features)) would remove this ~40 B.

---

## Job dispatch <a id="dispatch" />

How much it costs to **start** one job. The job here does almost nothing (its action is already
finished), so this suite
([`JobDispatchBenchmark.cs`](../src/Benchmark/Common/JobDispatchBenchmark.cs)) measures the
scheduler, not the real work. The `*_PerJob_*` rows divide the cost over many jobs, so they show the
true cost of one job.

- **v5.0.1** made a `Task` for every job (`Task.Factory.StartNew(async …)`).
- **v5.1.0** uses a fixed worker pool (a few workers take jobs from the queue), so the `Task` is made
  once per worker, not once per job.

### Results <a id="dispatch-results" />

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Method                               | Mean       | Allocated | Note                       |
|--------------------------------------|-----------:|----------:|----------------------------|
| ExecuteAsync_AsyncAction             |   163.7 ns |     144 B | ExecuteAsync alone¹        |
| Dispatch_TaskRun_AsyncAction         | 1,056.9 ns |     488 B | naive `Task.Run` (worse)   |
| Dispatch_StartNew_PerJob_AsyncAction |   445.1 ns |     216 B | **v5.0.1 per job**         |
| WorkerPool_PerJob_AsyncAction        |   187.5 ns |      72 B | **v5.1.0 per job**         |

¹ this row also counts one extra `Task` that the test itself creates (~72 B).

**Per job, v5.0.1 → v5.1.0: 216 B → 72 B (−67 %), ~445 ns → ~188 ns (−58 %).**

> Numbers depend on the machine (here: Arm64, .NET 8). Read them as relative, not exact.

### Takeaways <a id="dispatch-takeaways" />

- **The worker pool removes the extra Tasks made for every job** (~144 B per job). Only the `Task`
  inside `ExecuteAsync` is left (~72 B).
- **Just changing `StartNew` to `Task.Run` does not help** — it even uses more memory (488 B), because
  it captures the job in a closure. So the design had to change, not only the call.
- **`Task` → `ValueTask` would not help much here.** The job's own `Task` is only ~72 B, and when the
  job really runs async it still needs memory for the async state. The big cost was the dispatch, and
  that is gone now.

---

## Picking the next job <a id="next-job" />

When the recurring scheduler wakes up, it must find which job should run next.
This suite ([`SchedulerScanBenchmark.cs`](../src/Benchmark/Common/SchedulerScanBenchmark.cs))
compares the two ways over N jobs:

- **`Scan`** (up to v5.0.1): look at **every** job and pick the ready ones — O(n).
- **`Heap_Next`** (v5.1.0): keep jobs in a min-heap by next run time and take the top — O(log n).

In the test all N jobs are sleeping (the normal idle case), so `Scan` finds nothing but still has to
look at all of them.

### Results <a id="next-job-results" />

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Method    |     N | Mean          | Allocated |
|-----------|------:|--------------:|----------:|
| Scan      |  1000 |    69,193 ns  |     256 B |
| Heap_Next |  1000 |        41 ns  |       0 B |
| Scan      |  5000 |   343,500 ns  |     256 B |
| Heap_Next |  5000 |        47 ns  |       0 B |
| Scan      | 50000 | 3,653,915 ns  |     259 B |
| Heap_Next | 50000 |        62 ns  |       0 B |

> Numbers depend on the machine (here: Arm64, .NET 8). Read them as relative, not exact.

### Takeaways <a id="next-job-takeaways" />

- **`Scan` grows with the number of jobs** (O(n)): about 5× from 1k to 5k, and about 10× from 5k to
  50k. At 50 000 jobs one check takes ~3.65 ms — and the old scheduler did this on every tick, even
  when no job was ready.
- **`Heap_Next` stays almost flat** (O(log n)) and uses no memory: ~41 ns at 1k, ~62 ns at 50k.
- The new scheduler also **sleeps until the next job is due**, so when it is idle it does not waste CPU.

---

## Work queue (take) <a id="work-queue" />

How much it costs to take one job from the work queue when a job is already there (the normal case
under load). This suite ([`QueueHandoffBenchmark.cs`](../src/Benchmark/Common/QueueHandoffBenchmark.cs))
does one "enqueue + take" each time.

- **v5.0.1**: a lock + a `Queue` + a `TaskCompletionSource`. A take returned a `Task`, which is a new
  object every time.
- **v5.1.0**: a `Channel<T>`. A take returns a `ValueTask`. When a job is ready, that `ValueTask` is
  already done, so it makes no object.

An `object` is used in place of a job, so we measure the queue, not the job.

### Results <a id="work-queue-results" />

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Method             | Mean     | Allocated | Note          |
|--------------------|---------:|----------:|---------------|
| OldStyle_Lock_Task | 32.24 ns |      72 B | v5.0.1        |
| Channel_ValueTask  | 25.54 ns |       0 B | **v5.1.0**    |

> Numbers depend on the machine (here: Arm64, .NET 8). Read them as relative, not exact.

### Takeaways <a id="work-queue-takeaways" />

- **Taking a ready job makes no memory now** (72 B → 0 B) and is a bit faster (~32 ns → ~26 ns),
  because a ready `Channel` read is an already-done `ValueTask`.
- The `Channel` also gives **cancellation for free**: on shutdown a waiting take stops right away,
  instead of hanging.
- One code change: both queues use the same tool now (`Channel<T>`) instead of two different ones
  (`lock` + `TaskCompletionSource` for the queue, and `ConcurrentQueue` + `SemaphoreSlim` inside the
  recurring scheduler).

---

## Scheduler throughput <a id="throughput" />

Everything together: register/enqueue N jobs that each run once, start the scheduler, and wait for the
last result. This uses the whole pipeline at once — the worker pool, the heap producer (recurring),
and the `Channel` queue. Each job does no real work, so the number is the scheduler's own cost per job.
Files: [`RecurringThroughputBenchmark.cs`](../src/Benchmark/Common/RecurringThroughputBenchmark.cs),
[`QueueThroughputBenchmark.cs`](../src/Benchmark/Common/QueueThroughputBenchmark.cs).

`OperationsPerInvoke = N` makes it per-job: Mean is time per job, Allocated is memory per job (result +
payload + tasks). Throughput ≈ `1e9 / Mean(ns)` jobs per second.

This is a "big" benchmark — it starts real threads and makes a lot of garbage — so it is noisier than
the micro-benchmarks above. Read the throughput as a round number, not an exact one.

### Results <a id="throughput-results" />

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Scheduler | Mean (per job) | Allocated (per job) | ≈ Throughput   |
|-----------|---------------:|--------------------:|---------------:|
| Recurring |        107 ns  |        224 B        | ~9.3M jobs/sec |
| Queue     |        170 ns  |        279 B        | ~5.9M jobs/sec |

> N = 100,000 jobs per run, workers = ProcessorCount. Numbers depend on the machine (here: Arm64,
> .NET 8) and are noisy — relative, not exact.

### Takeaways <a id="throughput-takeaways" />

- With all the v5.1.0 changes, both schedulers do **millions of jobs per second** on one machine here
  (recurring ~9M/s, queue ~6M/s), allocating ~220–280 B per job.
- Most of that per-job memory is now the **result itself** — the `JobResult` object, its payload, and
  the job's `Task` — not the scheduler. The scheduler overhead (dispatch + queue + scheduling) is small
  now (see the sections above). That is why `JobResult` → `struct` is the next thing to look at if you
  want to push memory lower.
- The recurring scheduler is a bit faster per job here than the queue one, because its single producer
  feeds the workers, while the queue has many workers reading one channel at the same time.
