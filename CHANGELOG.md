#CHANGELOG
## v5.1.0
- [FS-11] recurring & queue schedulers now use a fixed worker pool instead of a Task per job — per-job allocation 216 B → 72 B (−67%), per-job dispatch ~445 ns → ~188 ns (−58%)
- [FS-11] recurring scheduler picks the next job from a min-heap instead of an O(n) scan of all registered jobs — one scheduling decision at 50k jobs: ~3.65 ms → ~62 ns; the producer now sleeps until the next job is due instead of polling
- [FS-11] **BREAKING**: both work queues use `System.Threading.Channels.Channel<T>` — `IQueue.NextAsync()` now returns `ValueTask<IQueueJob>` and takes a `CancellationToken`; a take on the hot (non-empty) path no longer allocates (72 B → 0 B) and shutdown can cancel a waiting take
- [FS-11] `ExecuteAsync`: replace sync-over-async `ActionJob().Result` with `await ActionJob()`
## v5.0.0
- **BREAKING**: drop `net6.0` and `net7.0` targets (both end-of-life); target `net8.0` (LTS) only
- the package keeps running on newer runtimes (net8/net9/net10) thanks to forward compatibility
## v2.2.3
- add default cron manager