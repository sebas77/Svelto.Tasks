# 02 · Simple Coroutine — ExtraLean Task

## Scenario

A simple countdown coroutine from 5 to 0 using the **ExtraLean** flavour of
Svelto.Tasks. ExtraLean works with plain `IEnumerator` (the same shape as Unity
coroutines) instead of `IEnumerator<TaskContract>`, so it is the lightest way to
write a coroutine when you do not need return values or continuations.

## Feature

**`ExtraLean.SteppableRunner`** — a steppable runner that accepts plain
`IEnumerator` tasks. The task yields `null` (the ExtraLean equivalent of "wait one
frame") between each step.

## When / Why to use it

- You have a very simple coroutine that just needs to spread work across steps.
- You do not need to return values from the task or chain it with `.Continue()`.
- You are porting Unity-style coroutines (`yield return null`) to a non-Unity
  context.
- You want the smallest task-contract overhead and do not need Lean return values or continuations.

## How it works

1. Create an `ExtraLean.SteppableRunner`.
2. Define a task as a plain `IEnumerator` that `yield return null` between steps.
3. Call `Task().RunOn(runner)` to enqueue it.
4. Call `runner.Step()` in a loop. Each step resumes the task until its next
   `yield return null` or `yield break`.
5. The runner stops having tasks when the enumerator finishes.

### The countdown bar

Each tick draws the current number with big block digits and a depleting bar that
shrinks as the countdown progresses.

## Key concepts

| Type / API | Purpose |
|---|---|
| `Svelto.Tasks.ExtraLean.SteppableRunner` | A steppable runner for plain `IEnumerator`. |
| `IEnumerator` (plain) | The task shape — no `TaskContract` needed. |
| `yield return null` | ExtraLean's "wait one step". |
| `.RunOn(runner)` | Enqueue the task on the runner. |
| `runner.Step()` | Advance every queued task by one yield. |

## Gotchas

- **ExtraLean can only yield a very limited set of things:** `null`,
  `TaskContract.Yield.It`, `TaskContract.Break.It`, `TaskContract.Break.AndStop`,
  or `yield break`. Yielding anything else (e.g. a number, a string) throws a
  `SveltoTaskException` at runtime.
- ExtraLean tasks **cannot return values** and **cannot use `.Continue()`** the
  way Lean tasks can. If you need return values or continuations, use the Lean
  flavour (`IEnumerator<TaskContract>`).
- `yield return null` is the idiomatic "wait one frame" in ExtraLean. It is
  semantically identical to `TaskContract.Yield.It` but avoids constructing a
  `TaskContract`.
- As with the Lean runner, **hold a reference** and **dispose** the runner.
