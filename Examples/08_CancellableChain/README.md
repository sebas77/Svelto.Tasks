# 08 · Cancellable Chain — stopping a chain with `Break.AndStop`

## Scenario

An operation chain **Load → Validate → Process**, launched by a `Parent` task. The
validation step fails and the failure must cancel the **entire** chain — `Process`
is skipped *and* `Parent` never reaches its final step.

## Feature

- **`Break.It`** — stops the failing task only, but lets its caller resume right
  after the `.Continue()`, so the caller can inspect what happened.
- **`Break.AndStop`** — breaks the current task and every waiting ancestor that
  continued it via `.Continue()`. The whole same-runner parent chain stops.

## When / Why to use it

- Pipelined operations where a failure in one stage must abort the whole pipeline.
- Validation / precondition chains where "stop everything now" is the desired semantics.
- Anytime a child failure should cancel the parent's remaining work.

## How it works

1. `Parent` yields `Chain().Continue()` and suspends on a continuation.
2. `Chain` yields `LoadStep().Continue()`, then `ValidateStep().Continue()`.
3. `ValidateStep` fails, records `validationFailed = true` and yields `Break.AndStop`.
4. The runner disposes `ValidateStep`, `Chain`, and `Parent` without advancing
   either parent again.
5. `ProcessStep` is never reached; `Parent`'s final `yield return 42` is skipped.

### Why not just `Break.AndStop` in ValidateStep?

That is exactly what this example does. `Break.AndStop` follows the complete
`.Continue()` chain, so `Chain` and `Parent` stop too.

### Chain diagram

```
┌────────┐    ┌──────────┐    ┌─────────┐
│  LOAD  │───▶│ VALIDATE │───▶│ PROCESS │
└────────┘    └──────────┘    └─────────┘
                  │ fails,
                   │ Break.AndStop     (never spawned)
                   ▼                       ▲
              Chain and Parent cancelled 💥
                  ▼
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `Break.AndStop` | Break self and all waiting `.Continue()` ancestors. |
| `Break.It` | Break self only; the caller resumes and can decide what to do next. |
| `.Continue()` | Schedule child on same runner; parent waits. |
| `.Complete(ms)` | Run an `IEnumerator<TaskContract>` synchronously to completion (uses a thread-local `SyncRunner`). |
| `TaskContract.Yield.It` | Yield one step. |

## Gotchas

- **`Break.AndStop` cascades through `.Continue()` ancestors.** It does not cross
  into work started with `.RunOn(otherRunner)`.
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
