# 08 · Cancellable Chain — forwarding a failure with `Break.AndStop`

## Scenario

An operation chain **Load → Validate → Process**, launched by a `Parent` task. The
validation step fails and the failure must cancel the **entire** chain — `Process`
is skipped *and* `Parent` never reaches its final step.

## Feature

- **`Break.It`** — stops the failing task only, but lets its caller resume right
  after the `.Continue()`, so the caller can inspect what happened.
- **`Break.AndStop`** — breaks the current task **and** the immediate parent that
  continued it via `.Continue()`. It propagates **exactly one level up**: a task
  killed by a child's break cannot run forwarding code of its own. To cancel N
  levels at once, each intermediate level must re-yield `Break.AndStop` itself —
  which is exactly what this example demonstrates.

## When / Why to use it

- Pipelined operations where a failure in one stage must abort the whole pipeline.
- Validation / precondition chains where "stop everything now" is the desired semantics.
- Anytime a child failure should cancel the parent's remaining work.

## How it works

1. `Parent` yields `Chain().Continue()` and suspends on a continuation.
2. `Chain` yields `LoadStep().Continue()`, then `ValidateStep().Continue()`.
3. `ValidateStep` fails, records `validationFailed = true` and yields `Break.It`.
4. Because `Break.It` doesn't kill callers, `Chain` resumes right after the
   continue, sees the flag and re-yields `Break.AndStop`.
5. The runner's `SveltoTaskWrapper` sees `breakMode == Break.AndStop` on the
   completing child (`Chain`) and completes `Parent` **without** advancing it.
6. `ProcessStep` is never reached; `Parent`'s final `yield return 42` is skipped.

### Why not just `Break.AndStop` in ValidateStep?

Then only `Chain` would stop. `Parent` would resume normally, because when a task
is cancelled through its child's break, its own enumerator never runs again — so it
has no chance to forward anything. One level per `Break.AndStop`, always.

### Chain diagram

```
┌────────┐    ┌──────────┐    ┌─────────┐
│  LOAD  │───▶│ VALIDATE │───▶│ PROCESS │
└────────┘    └──────────┘    └─────────┘
                  │ fails,
                  │ Break.It          (never spawned)
                  ▼                       ▲
              Chain resumes, checks flag  │
                  │                       │
                  │ Break.AndStop ───► Parent cancelled 💥
                  ▼
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `Break.AndStop` | Break self **and** the immediate parent that did `.Continue()` (one level). |
| `Break.It` | Break self only; the caller resumes and can decide what to do next. |
| `.Continue()` | Schedule child on same runner; parent waits. |
| `.Complete(ms)` | Run an `IEnumerator<TaskContract>` synchronously to completion (uses a thread-local `SyncRunner`). |
| `TaskContract.Yield.It` | Yield one step. |

## Gotchas

- **`Break.AndStop` propagates exactly ONE level up.** Multi-level cancellation
  requires every intermediate task to check the failure and re-yield
  `Break.AndStop` itself, as done here.
- **`Break.It` does not propagate.** It ends the current task and the parent
  resumes normally — which is precisely what makes the forwarding pattern possible.
- **`yield break` does not propagate either.** The parent continues as if the child
  completed normally.
- `Break.AndStop` only works with `.Continue()` (same-runner parent/child). It does
  **not** work with `.RunOn(otherRunner)` — the parent has no way to be notified back.
- The demo derives its final report from flags written while tasks run, instead of
  hardcoding the expected outcome: if break propagation ever regresses, the summary
  says so.
- The parent's `Current` after cancellation is **not** the value it would have yielded
  at the end; the final `yield return 42` is skipped entirely.
- `.Complete()` runs on a thread-local `SyncRunner`; it blocks the calling thread until
  the task finishes (or times out).
