# 04 · Continue Child Task — `.Continue()` on the Same Runner

## Scenario

A parent task delegates work to a child task **on the same runner** and waits for
it to finish before continuing. This is the simplest composition primitive in
Svelto.Tasks: one task suspends while another runs to completion, then resumes.

## Feature

**`.Continue()`** — an extension method on `IEnumerator` / `IEnumerator<TaskContract>`
that wraps the child in a `TaskContract` with the `sameRunnerContinuation` state.
When a parent yields it, the runner swaps to running the child on the **same**
runner and parks the parent until the child is done.

This is different from `.RunOn(runner)` (which would push the child onto a
*different* runner and return a `Continuation` to poll).

## When / Why to use it

- You want to structure a big task as a sequence of smaller, named sub-tasks.
- The sub-tasks must run on the **same** runner/thread as the parent (e.g. they
  touch main-thread-only state).
- You want the parent to block until each child completes, without the boilerplate
  of polling a `Continuation`.
- You are building a state machine out of coroutines.

## How it works

1. The parent yields `ChildTask().Continue()`.
2. The runner sees the `continueIt`/`sameRunnerContinuation` state and starts
   running the child **inline** on the current runner.
3. The parent is suspended until the child either completes, `yield break`s, or
   returns `Break.It`.
4. When the child finishes, the parent resumes on the next `Step()`.

### The flow diagram

The console draws a `PARENT` box, an arrow to a `CHILD` box, and an arrow back,
lighting up each side as control transfers so you can *see* the handoff happen
frame by frame.

## Key concepts

| Type / API | Purpose |
|---|---|
| `.Continue()` | Run child on the **same** runner; parent suspends until child done. |
| `.RunOn(otherRunner)` | Run child on a **different** runner; yields a `Continuation` to poll. |
| `TaskContract.Yield.It` | Suspend the current task one step. |
| `IEnumerator<TaskContract>` | Lean task shape. |
| `SteppableRunner` | Manually-stepped runner used for the demo. |

## Gotchas

- **Use `.Continue()` when the child should run on the same runner.** Use
  `.RunOn(runner)` only when you explicitly want a *different* runner (e.g. a
  background `MultiThreadRunner`).
- `.Continue()` does **not** return a `Continuation` you can poll — the parent is
  simply suspended. If you need to poll, use `.RunOn(otherRunner)` and yield the
  resulting `Continuation`.
- The child can itself yield `Continue()` on yet another child — nesting is
  supported, but each level adds a step of overhead.
- If the child `yield break`s immediately, the parent resumes on the **next**
  `Step()`, not in the same one.
- `.Continue()` is an extension method defined in the global
  `TaskRunnerExtensions` class (namespace `Svelto.Tasks` via the `using`).