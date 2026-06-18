#CHANGELOG
## v5.1.0
- recurring & queue schedulers now use a fixed worker pool instead of a Task per job — per-job allocation 216 B → 72 B (−67%), per-job dispatch ~445 ns → ~188 ns (−58%)
- `ExecuteAsync`: replace sync-over-async `ActionJob().Result` with `await ActionJob()`
## v5.0.0
- **BREAKING**: drop `net6.0` and `net7.0` targets (both end-of-life); target `net8.0` (LTS) only
- the package keeps running on newer runtimes (net8/net9/net10) thanks to forward compatibility
## v2.2.3
- add default cron manager