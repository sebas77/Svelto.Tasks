# Changelog
All notable changes to this project will be documented in this file.

## [2.0.0-preview.3] - 08-2026

This is the first changelog for Svelto.Tasks 2.0. It is a complete rewrite of the 1.5 codebase, not an incremental upgrade. The public API, package layout and runtime model have been redesigned; applications using 1.5 must migrate their task and runner code rather than expect drop-in compatibility.

### Breaking Changes

* Replace the 1.5 runner, scheduler, task-chain and Unity MonoBehaviour runner APIs with the 2.0 task, continuation and runner model
* Separate Lean `IEnumerator<TaskContract>` tasks from ExtraLean `IEnumerator` tasks, each with explicit yield restrictions and runner APIs
* Remove the legacy ServiceTasks API and built-in ObjectPool-based task-pool integration

### Added

* Add platform-agnostic `netstandard2.1` packaging with optional Unity integrations behind compiler defines
* Add `TaskContract`, `Continuation` and `TaskRunnerExtensions` for explicit task composition, cancellation and result passing
* Add Steppable, synchronous and dedicated multithreaded runners, together with reusable runner pools
* Add serial and parallel task collections, multithreaded task/job collections, and flow modifiers for serial, staggered and time-bounded execution
* Add iterator-block pooling for allocation-free reusable Lean and ExtraLean tasks
* Add .NET Task and ValueTask interop through awaiters and `TaskSynchronizationContext`; these APIs remain experimental
* Add an optional task-profiler plugin point and Unity profiler integration
* Add `MultiThreadedBurstParallelTaskCollection<TTask>` to split Burst range tasks into atomically claimed, fixed-size segments without allocating a wrapper for every segment
* Add reusable `Run()` enumerators for multithreaded parallel collections
* Add idle callbacks to `MultiThreadRunner` so parallel collections can feed idle workers and balance uneven workloads
* Add background-thread profiling scopes to the Unity profiler driver
* Add Examples 22 and 23 for `MultiThreadRunnerPool` dispatch and custom task-profiler drivers

### Changed

* Rebuild the scheduling core around explicit task state, continuations and deterministic runner lifecycle management
* Enable task profiling automatically in Debug builds; enable it in Release with `EnableTasksProfiler=true`
* Remove per-step allocations from profiler task-name lookup and cache task type names
* Make the Unity profiler driver Unity-only so plain .NET profiling plugins remain supported
* Use debug-only DBC preconditions when adding tasks or jobs to an active parallel collection

### Fixed

* Dispose queued parallel tasks and wait for in-flight scheduling before disposing workers
* Safely dispose partially constructed Burst parallel collections
