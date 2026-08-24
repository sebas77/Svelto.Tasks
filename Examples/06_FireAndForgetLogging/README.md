# 06 · Fire & Forget Logging — `.Forget()`

## Scenario

A game fires off a telemetry / logging task but does **not** want to wait for it.
The parent (gameplay logic) continues immediately while the child (telemetry) runs in
the background on the **same** runner.

## Feature

`IEnumerator<TaskContract>.Forget()` — marks a child iterator block as
fire-and-forget. The child is scheduled on the same runner as the parent, but the
parent does **not** suspend waiting for it. The parent's next `MoveNext` happens on
the very next step, before the child has run its body.

## When / Why to use it

- Telemetry, analytics, logging — anything that must happen but must not block gameplay.
- "Best effort" side effects where you don't care about the result or ordering relative
  to the parent.
- When you want to reuse the current runner/thread instead of spinning up a new one.

## How it works

1. The parent iterator yields `Child().Forget()`.
2. `TaskContract` stores the child enumerator with the `forgetLeanEnumerator` state.
3. The runner's `SveltoTaskWrapper` sees `isFireAndForget == true` and calls
   `leanSveltoTask.Run(_runner, child)` — the child is **added** to the runner queue
   but the parent is **not** given a continuation to wait on.
4. On the next `runner.Step()`, the parent continues (step `[4]`) and the child also
   gets its first `MoveNext` (step `[2]`).
5. A further step finishes the child (step `[3]`).

### Execution order

```
Step 1:  [1] parent starts
Step 2:  [4] parent resumes  +  [2] child starts   (same step, parent first)
Step 3:  [3] child finishes
```

Final order: **[1, 4, 2, 3]** — **not** [1, 2, 3, 4].

## Key concepts

| Type / API | Purpose |
|---|---|
| `SteppableRunner` (Lean) | A runner you step manually with `Step()`. Perfect for tests / console demos. |
| `IEnumerator<TaskContract>` | The shape of every Svelto iterator block. |
| `.Forget()` extension | Fire-and-forget: schedule child on same runner, parent does not wait. |
| `TaskContract.Yield.It` | Yield one step (come back next `Step()`). |
| `.RunOn(runner)` | Enqueue the root task on the runner. |

## Gotchas

- **Order is [1, 4, 2, 3]**, not [1, 2, 3, 4]. With `Forget()`, the parent continues
  on the next step *before* the child's body executes, because the child is merely
  queued, not immediately run.
- `Forget()` runs the child on the **same runner** as the parent. If you need the child
  on a *different* runner (e.g. a background thread), use `RunOn(otherRunner)` instead.
- The child is a **reference type** iterator (compiler-generated `IEnumerator`), so it
  is allocated; `Forget()` does **not** pool it. For pooled reuse see
  `IteratorBlockPool` + `Break.It` (Example 07).
- You must keep stepping the runner until the child is done, otherwise the fire-and-forget
  task is silently abandoned.