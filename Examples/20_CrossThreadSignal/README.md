# Example 20: Cross-Thread Signal — WaitForSignal

## Scenario

A background thread does work and signals the main thread when ready. The main thread yields the `.Wait()` enumerator on a `SteppableRunner`, ticking until the signal fires.

## Feature

**Cross-thread synchronization** — `WaitForSignal<T>` lets one thread wait for a signal from another, integrated into the Svelto.Tasks task pipeline.

## Why / When to Use

- **Background loading** — a background thread loads assets, signals the main thread when done.
- **Worker→main communication** — any pattern where a background thread completes work and the main thread needs to know.
- **Cross-runner coordination** — tasks on different runners/threads that need to synchronize.
- **Producer-consumer** — one thread produces data, signals; another thread consumes.

## How It Works

```
  ┌─────────────────────┐         ┌─────────────────────┐
  │   [MAIN THREAD]     │         │   [BG THREAD]       │
  │                     │         │                     │
  │  yield _signal.Wait()         │  for (i = 0; i <= 100)│
  │  ┌───────────────┐ │         │  ┌───────────────┐  │
  │  │ MoveNext():    │ │         │  │ do work (10%) │  │
  │  │  check volatile│ │         │  │ yield         │  │
  │  │  bool + timeout│ │         │  │ ...           │  │
  │  │  not signaled →│ │         │  │ 100% done     │  │
  │  │  return true   │ │         │  └───────┬───────┘  │
  │  └───────────────┘ │         │          │          │
  │  ...ticks...       │         │          ▼          │
  │  ┌───────────────┐ │         │  _signal.Signal()   │
  │  │ MoveNext():    │ │ ◄───── │  (volatile.Write)   │
  │  │  signal=true   │ │         │                     │
  │  │  return false  │ │         └─────────────────────┘
  │  └───────────────┘ │
  │  ✓ continue!       │
  └─────────────────────┘
```

1. `BackgroundWorkSignal` subclasses `WaitForSignal<BackgroundWorkSignal>` (the self-referential generic constraint forces a named subclass).
2. The background task runs on a `MultiThreadRunner`, does work, and calls `_signal.Signal()`.
3. `Signal()` does `Volatile.Write(ref _signal, true)` — thread-safe.
4. The main task yields `_signal.Wait()` (returns an `IEnumerator`). Each `MoveNext()` checks the volatile bool and a timeout.
5. When the signal fires, `MoveNext()` returns `false` → the main task continues past the wait.

## Key Concepts

| Type | Namespace | Purpose |
|------|-----------|---------|
| `WaitForSignal<T>` | `Svelto.Tasks.Enumerators` | Abstract base for cross-thread signaling |
| `.Signal()` | — | Signal from another thread (volatile write) |
| `.SignalBack()` | — | Bidirectional: signal back the other way |
| `.Wait()` | — | Returns `IEnumerator` to yield until signaled |
| `.WaitBack()` | — | Returns `IEnumerator` for the reverse direction |
| `MultiThreadRunner` | `Svelto.Tasks.Lean` | Background-thread runner |
| `SteppableRunner` | `Svelto.Tasks.Lean` | Main-thread manually-stepped runner |

## The Self-Referential Generic Constraint

```csharp
public abstract class WaitForSignal<T> where T : WaitForSignal<T>
```

This curious constraint (`T : WaitForSignal<T>`) forces you to create a **named subclass**:

```csharp
// You CANNOT do this — it's abstract:
// var signal = new WaitForSignal<...>();

// You MUST create a named subclass:
class BackgroundWorkSignal : WaitForSignal<BackgroundWorkSignal>
{
    public BackgroundWorkSignal(string name) : base(name) { }
}
```

**Why?** Readability and debugging. A field declared as `BackgroundWorkSignal` tells you exactly what it's for. Stack traces show the named type. This is a deliberate API design choice.

## Gotchas

- **Must subclass** — `WaitForSignal<T>` is abstract with a self-referential generic constraint. You cannot instantiate it directly; you must create a named subclass.
- **Timeout** — the default timeout is 1000ms. If the signal doesn't arrive in time, `MoveNext()` returns `false` anyway (timed out) and logs a warning. Set a longer timeout via the constructor: `base(name, timeout: 5000)`.
- **Auto-reset** — by default (`autoreset: true`), the signal auto-resets after completion, making it reusable. With `autoreset: false`, you must call `Reset()` manually before reusing. Note: `isDone()` only works with `autoreset: false` (it throws otherwise).
- **`startUnlocked`** — if `true`, the signal starts in the "signaled" state. Useful for initialization patterns where the first `.Wait()` should complete immediately.
- **Volatile semantics** — `Signal()` uses `Volatile.Write` and `MoveNext()` uses `Volatile.Read`, ensuring proper memory visibility across threads without explicit locks.
- **Bidirectional** — `SignalBack()` / `WaitBack()` provide a second channel for the reverse direction (main → background).

## API Reference

```csharp
// 1. Create a named subclass:
class LevelLoadSignal : WaitForSignal<LevelLoadSignal>
{
    public LevelLoadSignal() : base("LevelLoad", timeout: 10000) { }
}

// 2. Background thread signals:
_signal.Signal();

// 3. Main thread waits (in a task):
IEnumerator<TaskContract> WaitForLoad()
{
    var wait = _signal.Wait();
    while (wait.MoveNext())
        yield return TaskContract.Yield.It;
    // Signal received — proceed!
}

// 4. Constructor options:
new WaitForSignal("name", timeout: 5000, autoreset: true, startUnlocked: false);
```