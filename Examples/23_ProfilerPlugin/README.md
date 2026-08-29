# Example 23: Profiler Plugin (ITaskProfilerDriver + TaskProfiler)

## Scenario

You want to **measure what the scheduler is doing**: which tasks cost how much per
step, on which runner, without sprinkling `Stopwatch` calls through your task
bodies. This example installs a **custom profiler driver** into Svelto.Tasks and
shows the measurements it collects for fast, sleeping and jittery tasks — on both
the main thread and a background worker.

## Feature Demonstrated

The `Svelto.Tasks.Profiler` plugin point:

- `TaskProfiler.Driver` — assign any `ITaskProfilerDriver` and every task step on
  every runner is funneled through it with balanced `Begin*/End*` scopes:
  - `BeginRunner` / `EndRunner` around each runner pass
  - `BeginTask` / `EndTask(runner, task, elapsedMs)` around each single task step
- The built-in `TaskProfiler` aggregates per-pass min/avg/max per task
  (`TaskInfo`) independently of the driver — read it back with
  `CopyAndUpdate(ref TaskInfo[])`.
- Unity reference plugin: `UnityTaskProfilerDriver` bridges the same scopes to
  Unity Profiler markers/counters and installs itself automatically
  (`RuntimeInitializeOnLoadMethod`), with an editor window behind the
  `Tasks Profiler` menu item.

## Enabling the profiler (plain .NET)

The profiler is **compiled out of Release builds by default** — when the define is
off, the runner stepping path is exactly the un-instrumented one. The
instrumentation adds a lock + stopwatch per task step, so shipping it is strictly
opt-in:

- **Debug builds are instrumented automatically** (both the library and this
  example define `TASKS_PROFILER_ENABLED` when `$(Configuration)` is `Debug`) —
  a plain IDE run (F5 in Rider/VS) measures out of the box.
- **Release builds** need the opt-in flag:

```bash
dotnet build Packages/com.sebaslab.svelto.tasks/Svelto.Tasks/Svelto.Tasks.csproj -c Release -p:EnableTasksProfiler=true
dotnet run --project Examples/23_ProfilerPlugin -c Release -p:EnableTasksProfiler=true
```

`EnableTasksProfiler=true` defines `TASKS_PROFILER_ENABLED` in **both** the
library and this example (it flows as a global property to the referenced
project). The define is baked in at compile time — toggling it only makes sense
together with a rebuild. In Unity you add `TASKS_PROFILER_ENABLED` through the
menu item (or Player Settings scripting defines) instead.

## How It Works

1. `TaskProfiler.Driver = new ConsoleProfilerDriver();` — install the plugin.
2. Run a `SteppableRunner` with three tasks of different cost (µs-fast, ~2 ms
   sleep, 0–3 ms jitter) for 60 frames, plus a task on a `MultiThreadRunner`.
3. The driver receives every step: it aggregates total/min/max/steps per
   `(task, runner)` and renders a table at the end.
4. `TaskProfiler.CopyAndUpdate(ref infos)` fetches the scheduler's own per-pass
   statistics (average/min/max over the last 32 passes) for comparison.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `ITaskProfilerDriver` | `Svelto.Tasks.Profiler` | Plugin interface receiving runner/task scopes |
| `TaskProfiler.Driver` | `Svelto.Tasks.Profiler` | Install point (thread-safe property) |
| `TaskProfiler.MonitorUpdateDuration` | internal | Where runners feed the profiler |
| `TaskInfo` | `Svelto.Tasks.Profiler` | Built-in per-pass min/avg/max aggregate |
| `UnityTaskProfilerDriver` | `Svelto.Tasks.Profiler` (Unity) | Reference backend → Unity Profiler |

## Gotchas

- **Drivers must be thread-safe.** `EndTask` can arrive concurrently from several
  worker threads; the scheduler does not serialize the callbacks (this demo's
  driver locks).
- **Opt-in overhead is real.** With `TASKS_PROFILER_ENABLED`, every step pays a
  lock + `Stopwatch` restart/stop, plus name normalization on first sight of a
  task. Never ship the define in a production build.
- **Per-pass, not per-frame.** The built-in `TaskInfo` statistics are reset at the
  start of each runner pass (`ResetDurations`), so "avg" is per `Step()` of that
  runner, not per wall-clock frame.
- **The thread hook is Unity-only.** `BeginWorkerThread`/`EndWorkerThread` ride on
  an internal interface implemented by the Unity driver; custom .NET drivers only
  see the runner/task scopes (the background task in this demo still measures
  correctly — `EndTask` simply arrives on the worker thread).
- `NormalizeTaskName` strips compiler-generated iterator noise
  (`Ns.Type+<Method>d__3` → `Type.Method`), so table rows stay readable.
