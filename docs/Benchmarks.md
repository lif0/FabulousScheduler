# Benchmarks

Micro-benchmarks for FabulousScheduler, powered by [BenchmarkDotNet](https://benchmarkdotnet.org/).
The benchmark project lives in [`src/Benchmark/Common`](../src/Benchmark/Common).

### 📖 Contents

- [How to run](#how-to-run)
- [JobResult&lt;TOk, TFail&gt;](#jobresult)
  - [Results](#jobresult-results)
  - [Takeaways](#jobresult-takeaways)
- [Job dispatch](#dispatch)
  - [Results](#dispatch-results)
  - [Takeaways](#dispatch-takeaways)
- [Scheduler throughput](#throughput) (future)

---

## How to run <a id="how-to-run" />

Benchmarks must be run in **Release**. Use the CLI filter to pick a suite:

```shell
# all benchmarks
dotnet run -c Release --project src/Benchmark/Common -- --filter *

# only the JobResult suite
dotnet run -c Release --project src/Benchmark/Common -- --filter *JobResult*

# list everything without running
dotnet run -c Release --project src/Benchmark/Common -- --list flat
```

---

## JobResult&lt;TOk, TFail&gt; <a id="jobresult" />

[`JobResult<TOk, TFail>`](Core.md#jobresult) is the value every job returns. It is declared
as a `class`, so each result is a heap allocation. This suite
([`JobResultBenchmark.cs`](../src/Benchmark/Common/JobResultBenchmark.cs)) measures both that
allocation and the cost of the success/fail dispatch (`Do`, `Match`, `JobID`). The success/fail
payloads and the delegates are pre-created in `[GlobalSetup]`, so the numbers reflect
`JobResult` itself, not the payloads.

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

> Numbers are machine-dependent (here: Arm64, .NET 8). Treat them as relative, not absolute.

### Takeaways <a id="jobresult-takeaways" />

- **Creating a result costs ~40 B of managed heap per job** — because `JobResult<TOk, TFail>`
  is a reference type. At high throughput (tens of thousands of jobs/sec) this is the dominant
  GC pressure of the result path.
- **Dispatch is essentially free**: `Do`, `Match<T>` and `JobID` are sub-nanosecond and allocate
  nothing. The success/fail branching is not a bottleneck.
- This motivates the open items in the [roadmap](../README.md#features): turning `JobResult`
  into a `struct` (and the `Task` → `ValueTask` refactor) would remove the 40 B/op allocation.

---

## Job dispatch <a id="dispatch" />

How much it costs to start one job. This suite
([`JobDispatchBenchmark.cs`](../src/Benchmark/Common/JobDispatchBenchmark.cs)) runs a real
`BaseRecurringJob` whose action is an already-completed task, so the numbers reflect the framework
path, not payload work. The `*_PerJob_*` rows use `OperationsPerInvoke` so they report the **true
per-job allocation** (the benchmark's own wrapper task is amortized away).

In **v5.0.1** the schedulers dispatched a `Task` per job (`Task.Factory.StartNew(async …)`).
In **v5.1.0** they use a fixed worker pool (N workers drain the queue), so the dispatch `Task` is
allocated once per worker instead of once per job.

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
| Dispatch_StartNew_PerJob_AsyncAction |   445.1 ns |     216 B | **v5.0.1 per-job**         |
| WorkerPool_PerJob_AsyncAction        |   187.5 ns |      72 B | **v5.1.0 per-job**         |

¹ non-amortized, so it includes the benchmark's own +1 wrapper task (~72 B).

**Per-job, v5.0.1 → v5.1.0: 216 B → 72 B (−67 %), ~445 ns → ~188 ns (−58 %).**

> Numbers are machine-dependent (here: Arm64, .NET 8). Treat them as relative, not absolute.

### Takeaways <a id="dispatch-takeaways" />

- **The worker pool removes the per-job dispatch overhead** (`Task<Task>` + `Unwrap` + the
  async state-machine box) — ~144 B/job — leaving only `ExecuteAsync`'s own `Task<JobResult>` (~72 B).
- **Swapping the dispatch primitive does not help**: `Task.Run` instead of `StartNew` allocates *more*
  (488 B vs 400 B) because of the closure capture. The fix has to be architectural.
- **`Task` → `ValueTask` would not move the needle**: `ExecuteAsync`'s own task is only ~72 B, and on
  the async path it suspends (a `ValueTask` still allocates the state-machine box). The per-job cost
  lived in the dispatch, which is now gone.

---

## Scheduler throughput <a id="throughput" /> (future)

End-to-end throughput benchmarks for the recurring and queue schedulers are not implemented yet.
