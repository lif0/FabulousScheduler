# Benchmarks

Small benchmarks for FabulousScheduler, built with [BenchmarkDotNet](https://benchmarkdotnet.org/).
The project lives in [`src/Benchmark/Common`](../src/Benchmark/Common).

Every number on this page comes from one machine (Arm64, .NET 8). Treat them as relative: the ratios
and the shape (does it grow with N? does it allocate?) matter more than the exact nanoseconds.

### 📖 Contents

- [How to run](#how-to-run)
- [What got better by version](#by-version)
- [JobResult&lt;TOk, TFail&gt;](#jobresult)
- [Job dispatch](#dispatch) (changed in v5.1.0)
- [Picking the next job](#next-job) (changed in v5.1.0)
- [Work queue (take)](#work-queue) (changed in v5.1.0)
- [Scheduler throughput](#throughput)

---

## How to run <a id="how-to-run" />

Run them in Release. Pick a suite with the CLI filter:

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

A quick map of where things got faster. All of it is measured in the suites further down.

### v5.1.0

Starting a job got cheaper. The scheduler used to make a fresh `Task` for every job. Now a small pool
of workers takes jobs one at a time, so a `Task` is created once per worker instead of once per job.

| Per job | v5.0.1 | v5.1.0 | Better by |
|---------|-------:|-------:|----------:|
| Memory  |  216 B |   72 B |   −67 %   |
| Time    | ~445 ns| ~188 ns|   −58 %   |

Finding the next job got a lot cheaper. The recurring scheduler used to look at every registered job
on each tick to find the ready ones. Now it keeps them in a min-heap ordered by next run time, so the
next job is just the top of the heap.

| Find next job | v5.0.1 (look at all) | v5.1.0 (heap) |
|---------------|---------------------:|--------------:|
| 1 000 jobs    |              ~69 µs  |       ~41 ns  |
| 50 000 jobs   |            ~3.65 ms  |       ~62 ns  |

It also sleeps until the next job is actually due instead of waking up over and over, so an idle
scheduler stops burning CPU.

Taking a job off the queue got cheaper too. Both work queues use a `Channel` now. When a job is
already waiting, taking it hands back a `ValueTask` that is already finished, so nothing is allocated.

| Take one job (job ready) | v5.0.1 | v5.1.0 |
|--------------------------|-------:|-------:|
| Memory                   |   72 B |    0 B |
| Time                     | ~32 ns | ~26 ns |

This one came with a breaking change: `IQueue.NextAsync()` now returns `ValueTask<IQueueJob>` and
takes a `CancellationToken`. It only matters if you wrote your own `IQueue`.

And a small clean-up along the way: `ExecuteAsync` no longer blocks on `ActionJob().Result`, it
awaits. Same speed, just safer.

### v5.0.1

The version everything above is measured against.

---

## JobResult&lt;TOk, TFail&gt; <a id="jobresult" />

[`JobResult<TOk, TFail>`](Core.md#jobresult) is the value every job returns. It's a `class`, so each
result is a new object on the heap. This suite
([`JobResultBenchmark.cs`](../src/Benchmark/Common/JobResultBenchmark.cs)) measures that allocation
and the cost of reading the result (`Do`, `Match`, `JobID`). The success/fail values and the delegates
are built once in `[GlobalSetup]`, so the numbers are about `JobResult` itself, not the work inside.

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

Creating a result costs about 40 B, because it's a class. At tens of thousands of jobs a second that
is the main memory cost of the result path. Reading it is basically free: `Do`, `Match<T>` and `JobID`
are sub-nanosecond and allocate nothing, so the success/fail branching is never the bottleneck.
Turning `JobResult` into a `struct` (on the [roadmap](../README.md#features)) would take the 40 B away.

---

## Job dispatch <a id="dispatch" />

What it costs to start one job. The job here does nothing useful (its action is already finished), so
this suite ([`JobDispatchBenchmark.cs`](../src/Benchmark/Common/JobDispatchBenchmark.cs)) measures the
scheduler, not the work. The `*_PerJob_*` rows spread the cost over many jobs, so they show the real
cost of a single one.

In v5.0.1 the scheduler made a `Task` per job (`Task.Factory.StartNew(async …)`). In v5.1.0 a fixed
worker pool takes jobs off the queue, so the `Task` is created once per worker, not once per job.

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
| Dispatch_StartNew_PerJob_AsyncAction |   445.1 ns |     216 B | v5.0.1 per job             |
| WorkerPool_PerJob_AsyncAction        |   187.5 ns |      72 B | v5.1.0 per job             |

¹ this row also counts one extra `Task` the test itself creates (~72 B).

Per job, v5.0.1 to v5.1.0: 216 B down to 72 B (−67 %), ~445 ns down to ~188 ns (−58 %).

The worker pool drops the extra Tasks that were spun up per job (~144 B), leaving only the `Task` that
lives inside `ExecuteAsync` (~72 B). Note that swapping `StartNew` for `Task.Run` on its own makes
things worse, not better (488 B), because the job gets captured in a closure. The win came from
changing the design, not the call. And `Task` → `ValueTask` wouldn't move the needle here: the job's
own `Task` is only ~72 B, and a job doing real async work still needs memory for its async state. The
expensive part was the dispatch, and that's gone.

---

## Picking the next job <a id="next-job" />

When the recurring scheduler wakes up it has to find which job runs next. This suite
([`SchedulerScanBenchmark.cs`](../src/Benchmark/Common/SchedulerScanBenchmark.cs)) puts the two
approaches side by side over N jobs:

- `Scan` (up to v5.0.1): look at every job and pick the ready ones. O(n).
- `Heap_Next` (v5.1.0): keep jobs in a min-heap by next run time and take the top. O(log n).

In the test all N jobs are sleeping, which is the normal idle case, so `Scan` finds nothing yet still
has to walk all of them.

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

`Scan` grows with the job count: roughly 5× from 1k to 5k, roughly 10× from 5k to 50k. At 50 000 jobs
a single check takes about 3.65 ms, and the old scheduler paid that on every tick even when nothing was
ready. `Heap_Next` stays almost flat (41 ns to 62 ns across that whole range) and allocates nothing. On
top of that the new scheduler sleeps until the next job is due, so an idle scheduler with a big job set
no longer spends CPU re-checking.

---

## Work queue (take) <a id="work-queue" />

What it costs to take one job from the work queue when a job is already there, which is the usual case
under load. This suite ([`QueueHandoffBenchmark.cs`](../src/Benchmark/Common/QueueHandoffBenchmark.cs))
does one enqueue plus one take each iteration. An `object` stands in for the job so we measure the
queue, not the job.

v5.0.1 used a lock plus a `Queue` plus a `TaskCompletionSource`; a take handed back a `Task`, a new
object every time. v5.1.0 uses a `Channel`; a take hands back a `ValueTask`, and when a job is ready
that `ValueTask` is already finished, so nothing is allocated.

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Method             | Mean     | Allocated | Note     |
|--------------------|---------:|----------:|----------|
| OldStyle_Lock_Task | 32.24 ns |      72 B | v5.0.1   |
| Channel_ValueTask  | 25.54 ns |       0 B | v5.1.0   |

Taking a ready job now allocates nothing (72 B down to 0 B) and is a touch faster (~32 ns to ~26 ns),
because a ready `Channel` read is an already-finished `ValueTask`. The `Channel` also handles
cancellation for you: on shutdown a waiting take stops immediately instead of hanging. As a bonus, both
queues use the same primitive now, where before there were two (`lock` + `TaskCompletionSource` for the
queue, `ConcurrentQueue` + `SemaphoreSlim` inside the recurring scheduler).

---

## Scheduler throughput <a id="throughput" />

The whole thing end to end: register or enqueue N jobs that each run once, start the scheduler, and
wait for the last result. This pulls in everything at once, the worker pool, the heap producer (for
recurring), and the `Channel` queue. The jobs do no real work, so the number is the scheduler's own
cost per job. Files:
[`RecurringThroughputBenchmark.cs`](../src/Benchmark/Common/RecurringThroughputBenchmark.cs),
[`QueueThroughputBenchmark.cs`](../src/Benchmark/Common/QueueThroughputBenchmark.cs).

`OperationsPerInvoke = N` reports it per job: Mean is time per job, Allocated is memory per job (the
result, its payload, and the tasks). Throughput is roughly `1e9 / Mean(ns)` jobs a second. This is a
macro-benchmark that spins up real threads and makes a lot of garbage, so it's noisier than the micros
above; the throughput is a ballpark, not a precise figure.

```
BenchmarkDotNet v0.13.12, Ubuntu 24.04.4 LTS (Noble Numbat)
.NET SDK 8.0.128
  [Host]   : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
  .NET 8.0 : .NET 8.0.28 (8.0.2826.26413), Arm64 RyuJIT AdvSIMD
```

| Scheduler | Mean (per job) | Allocated (per job) | Throughput (approx) |
|-----------|---------------:|--------------------:|--------------------:|
| Recurring |        107 ns  |        224 B        |      ~9.3M jobs/sec |
| Queue     |        170 ns  |        279 B        |      ~5.9M jobs/sec |

N = 100,000 jobs per run, with ProcessorCount workers.

With the v5.1.0 changes both schedulers push millions of jobs a second on a single machine here
(recurring ~9M/s, queue ~6M/s), at roughly 220 to 280 B per job. The interesting part is where that
memory goes: most of it is now the result itself (the `JobResult`, its payload, and the job's `Task`),
not the scheduler. The dispatch, queue and scheduling overhead is small now, which is exactly why
`JobResult` → `struct` is the next thing worth looking at for memory. Recurring comes out a little
faster per job than queue here, because a single producer feeds its workers while the queue has all
the workers reading one channel at once.
