# Example 19: Delayed Spawn — WaitForSecondsEnumerator

## Scenario

A game level starts, then 2 seconds later an enemy spawns. We run a `WaitForSecondsEnumerator` on a `SteppableRunner` and tick it manually, showing a live countdown to the spawn event.

## Feature

**Time-based task yielding** — the `WaitForSecondsEnumerator` yields `TaskContract.Yield.It` on each tick until a target time is reached, then `MoveNext()` returns `false` (done).

## Why / When to Use

- **Delayed spawns** — enemies, power-ups, events that trigger after a fixed delay.
- **Timed sequences** — cutscene beats, dialogue delays, wave-based gameplay.
- **Cooldowns** — ability cooldowns implemented as tasks.
- **Any "wait N seconds then do X" pattern** in a task-based pipeline.

## How It Works

```
  Start: DateTime.UtcNow + 2.0s = target time
         │
    ┌─────┴──────────────────────────────────────┐
    │  MoveNext() called each Step():             │
    │                                            │
    │  tick 1: now < target → return true (yield)│
    │  tick 2: now < target → return true (yield)│
    │  ...                                       │
    │  tick N: now >= target → Reset, return false│
    │                                            │
    │  → task continues past the while loop      │
    └────────────────────────────────────────────┘
         │
         ▼
    👾 ENEMY SPAWNED!
```

1. `new WaitForSecondsEnumerator(2.0f)` captures a target time: `DateTime.UtcNow + 2 seconds`.
2. Each `MoveNext()` call checks: is `DateTime.UtcNow >= target`? If not, returns `true` (keep yielding).
3. When the time passes, `MoveNext()` calls `Reset()` and returns `false` (done).
4. The task's `while` loop exits, and execution continues past the wait.

## Key Concepts

| Type | Namespace | Purpose |
|------|-----------|---------|
| `WaitForSecondsEnumerator` | `Svelto.Tasks.Enumerators` | Class-based time wait (allocates) |
| `ReusableWaitForSecondsEnumerator` | `Svelto.Tasks.Enumerators` | Struct-based time wait (zero-alloc, reusable) |
| `SteppableRunner` | `Svelto.Tasks.Lean` | Manually-stepped runner |
| `.Step()` | — | Tick all tasks once |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one tick |

## Class vs Struct — allocation comparison

| Type | Kind | Allocates? | Resettable? | Reusable? |
|------|------|-----------|-------------|-----------|
| `WaitForSecondsEnumerator` | class | Yes (heap) | Yes (`Reset()` / `Reset(float)`) | Yes |
| `ReusableWaitForSecondsEnumerator` | struct | **No** (stack) | Yes (`Reset()` / `Reset(float)`) | Yes |

**For zero-allocation:** use `ReusableWaitForSecondsEnumerator` (a struct). It can be `Reset()` and reused with a new duration — no GC pressure.

## Gotchas

- `WaitForSecondsEnumerator` uses `DateTime.UtcNow` — it's wall-clock time, not "game time." If you pause the runner, real time still passes. When you resume, the wait may have already elapsed.
- The enumerator captures the target time on the **first** `MoveNext()` call (via an `_init` flag). If you `Reset()` and reuse, it re-captures on the next first call.
- `Reset(float seconds)` lets you change the duration without creating a new instance.
- `ReusableWaitForSecondsEnumerator.IsDone()` calls `MoveNext()` internally and returns the negation — convenient for polling outside a task.
- These enumerators yield `TaskContract.Yield.It`, so they're **Lean** tasks (`IEnumerator<TaskContract>`). They won't work with ExtraLean runners.

## API Reference

```csharp
// Class version (allocates):
var wait = new WaitForSecondsEnumerator(2.0f);
while (wait.MoveNext())
    yield return TaskContract.Yield.It;
// 2 seconds have passed — continue

// Struct version (zero allocation):
var reusable = new ReusableWaitForSecondsEnumerator(2.0f);
while (reusable.MoveNext())
    yield return TaskContract.Yield.It;
// reuse:
reusable.Reset(5.0f);  // new duration, no allocation

// The struct also has a convenience method:
if (reusable.IsDone()) { /* time's up */ }
```