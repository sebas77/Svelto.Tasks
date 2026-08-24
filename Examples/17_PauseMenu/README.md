# Example 17: Pause Menu — Runner Pause/Resume

## Scenario

A game runs tasks on a `MultiThreadRunner`. When the pause menu opens, all task processing freezes. When it closes, tasks resume exactly where they left off — no state lost, no tasks dropped.

## Feature

**Runner Pause/Resume** — freezing and unfreezing task execution without losing state.

`MultiThreadRunner.Pause()` freezes all task states (they stay in the queue but don't execute). `Resume()` unfreezes them, and they continue from exactly where they stopped.

## Why / When to Use

- **Pause menu** in a game — freeze all background AI, animation, loading tasks when the player opens a menu.
- **Debugging** — pause a runner to inspect task state mid-execution.
- **Throttling** — temporarily pause a background worker without tearing it down.
- **Conditional execution** — pause/resume based on game state (cutscenes, loading screens).

## How It Works

```
        ▶ RUNNING                    ⏸ PAUSED
     ┌──────────┐                 ┌──────────┐
     │ counter:42│                 │ counter:42│  ← frozen!
     │ 🔥 ticking│                 │ ❄ frozen  │
     └──────────┘                 └──────────┘
          │                            │
     runner.Pause()               runner.Resume()
          │                            │
          ▼                            ▼
     tasks freeze                  tasks resume
     (volatile flag)               (volatile flag)
     thread spins/blocks            thread unblocks
```

1. A counting task runs on a `MultiThreadRunner`, incrementing a shared counter each tick.
2. `runner.Pause()` sets an internal volatile flag. The runner's background thread enters a spin/wait lock — tasks don't execute.
3. While paused, the counter stays **frozen** at whatever value it had. We verify this by snapshotting and checking it doesn't change.
4. `runner.Resume()` clears the flag. The thread unlocks, tasks resume, and the counter climbs again.

## Key Concepts

| Type | Namespace | Purpose |
|------|-----------|---------|
| `MultiThreadRunner` | `Svelto.Tasks.Lean` | Background-thread runner with pause/resume |
| `.Pause()` | — | Freezes task execution (tasks stay in queue) |
| `.Resume()` | — | Unfreezes task execution |
| `.Stop()` | — | Flushes tasks until empty (they run to completion) |
| `.WaitForTasksDone(timeout)` | — | Blocks until all tasks complete (with optional timeout) |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one tick to the runner |

## Pause vs Stop — the critical distinction

| Operation | Tasks execute? | Task states | New tasks accepted? | Runner reusable? |
|-----------|---------------|------------|---------------------|-----------------|
| `Pause()` | No (frozen) | **Preserved** | Yes | Yes (after `Resume()`) |
| `Resume()` | Yes | — | Yes | Yes |
| `Stop()` | Flushes in-flight | **Drained** | Yes (queued, not processed) | Yes |
| `Dispose()` | Disposes all | **Destroyed** | Throws | No (dead) |

**Pause = freeze.** Tasks stay in the queue with their state intact. When you resume, they pick up exactly where they paused.

**Stop = flush.** Tasks are flushed — they run to completion (or stop naturally). The queue drains. Ever-looping tasks are forced to stop and terminate.

## Gotchas

- `Pause()` is **thread-safe** — you can call it from any thread. The runner uses a lock-free spin mechanism (`_quickThreadSpinning`) for reactive pause/resume.
- After `Pause()`, the background thread doesn't die — it enters a low-CPU spin/wait state, ready to resume instantly.
- `Pause()` does NOT dispose tasks. `Dispose()` does. If you want to restart cleanly, use `Stop()` (drain) then add new tasks — or `Dispose()` and create a new runner.
- The `MultiThreadRunner` constructor starts the background thread immediately. It begins in a **paused** state internally until `UseFlowModifier` is called (which the Lean variant does in its constructor).
- `WaitForTasksDone(timeout)` returns `true` if all tasks completed within the timeout, `false` if they're still running.

## API Reference

```csharp
var runner = new MultiThreadRunner("GameRunner");

// Run a task
MyTask().RunOn(runner);

// Pause — freeze all tasks
runner.Pause();

// ... tasks are frozen, counter doesn't change ...

// Resume — unfreeze
runner.Resume();

// Wait for all tasks to finish (with 1s timeout)
runner.WaitForTasksDone(1000);

// Clean up
runner.Dispose();
```