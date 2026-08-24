# 01 · Game Loop — Lean Task + `SteppableRunner`

## Scenario

A simulated game loop ticks a `SteppableRunner` once per "frame". A task counts
frames and yields between each count, just like an in-game coroutine that must
spread its work across multiple frames instead of blocking the main thread.

## Feature

**Lean `SteppableRunner`** (from `Svelto.Tasks.Lean`) — a runner you advance
**manually** with `runner.Step()`. Nothing happens until you call `Step()`, so it
is the ideal runner for a deterministic game loop, a test harness, or a console
demo where you want to control exactly when each "frame" happens.

The task is an `IEnumerator<TaskContract>` that uses **`TaskContract.Yield.It`**
to suspend itself until the next step.

## When / Why to use it

- You need frame-by-frame control (a game loop, a simulation tick, a turn-based
  game).
- You want to unit-test coroutine logic deterministically without real time.
- You are on a platform without a player loop (a console app, a server) and you
  must drive the scheduler yourself.
- You want zero background threads — everything runs on the calling thread.

## How it works

1. Create a `Lean.SteppableRunner` and **keep it in a field/variable**.
2. Define a task as `IEnumerator<TaskContract>` and `yield return TaskContract.Yield.It`
   wherever you want to suspend.
3. Call `Task().RunOn(runner)` to enqueue the task. It does **not** run yet.
4. Call `runner.Step()` once per frame. Each `Step()` resumes every queued task
   until its next `Yield.It` (or completion).
5. `runner.hasTasks` becomes `false` when all tasks are done.
6. `Dispose()` the runner when finished.

### The spinner

Each frame prints the frame number with a rotating spinner character
(`|`, `/`, `─`, `\`) so you can *see* the runner being stepped.

## Key concepts

| Type / API | Purpose |
|---|---|
| `Svelto.Tasks.Lean.SteppableRunner` | A runner advanced manually via `Step()`. |
| `IEnumerator<TaskContract>` | The shape of every Lean Svelto iterator block. |
| `TaskContract.Yield.It` | Suspend the task until the next `Step()`. |
| `.RunOn(runner)` | Enqueue the root task on the runner (returns a `Continuation`). |
| `runner.Step()` | Tick every queued task by one yield. |
| `runner.hasTasks` | `true` while at least one task is still running. |

## Gotchas

- **Hold a reference to the runner.** If the runner is GC'd while tasks are still
  queued, a finalizer logs a warning and kills it. The framework does **not** keep
  a strong reference for you.
- **`Dispose()` the runner** when done, otherwise its finalizer will fire later
  and print a warning.
- `Yield.It` yields exactly **one** `Step()`. If you forget to yield inside a loop,
  the task can run forever in a single step and starve everything else.
- `RunOn` returns a `Continuation` you can poll with `.isRunning`, but for a
  steppable runner you usually just check `runner.hasTasks`.
- `Step()` returns `true` while there are still tasks to process.