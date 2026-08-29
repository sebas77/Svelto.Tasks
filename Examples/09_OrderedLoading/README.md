# 09 · Ordered Loading — `SerialTaskCollection`

## Scenario

Game level loading with three stages — **Download → Parse → Initialize** — that must
run strictly one after another. Stage B does not start until Stage A has fully
completed.

## Feature

`SerialTaskCollection` — a `TaskCollection` that runs its tasks **in order**. The next
task is not touched until the current one finishes (returns `false` from `MoveNext` or
yields `Break.It`).

## When / Why to use it

- Sequential pipelines where each stage depends on the previous one's output.
- Loaders: download → deserialize → initialize.
- Any "do A, then B, then C" workflow where overlap is incorrect.

## How it works

1. Create the collection:
   ```csharp
   var serial = new SerialTaskCollection("LevelLoader");
   ```
2. Add tasks **before** running:
   ```csharp
   serial.Add(DownloadStage());
   serial.Add(ParseStage());
   serial.Add(InitStage());
   ```
3. Run it with `.Complete(ms)` (synchronous, uses a thread-local `SyncRunner`) or
   `.RunOn(runner)` + stepping.
4. `SerialTaskCollection.RunTasksAndCheckIfDone()` iterates the internal list of stacks.
   For each task it calls `ProcessStackAndCheckIfDone` repeatedly until that task
   yields `doneIt` (finished) — only then does it advance `_stackOffset` to the next
   task. If a task `yieldIt`s (returns `TaskContract.Yield.It`), the collection returns
   `false` from its own `MoveNext` and resumes the **same** task on the next step.

### Pipeline diagram

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│ DOWNLOAD │ ──▶ │  PARSE   │ ──▶ │   INIT   │
└──────────┘     └──────────┘     └──────────┘
   fills first       waits              waits
   then done ─────▶  fills              waits
                      then done ─────▶  fills
                                        then done ✅
```

## Key concepts

| Type / API | Purpose |
|---|---|
| `SerialTaskCollection` | Ordered task list; tasks run one at a time, in insert order. |
| `.Add(IEnumerator<TaskContract>)` | Enqueue a task. Must be called **before** running. |
| `.Complete(ms)` | Run synchronously to completion (blocks calling thread). |
| `.RunOn(runner)` | Enqueue on a steppable runner; step manually. |
| `TaskContract.Yield.It` | Yield one step; the collection resumes the same task next step. |
| `.Reset()` / `.Clear()` | Reset tasks for re-execution / remove all tasks. |

## Gotchas

- **Cannot `Add()` while running.** The collection sets `isRunning = true` on the first
  `MoveNext` and `Add()` throws a `PreconditionException` if called mid-run. Build the
  whole collection first, then run.
- Tasks run **strictly in order** — stage B's first `MoveNext` happens only after stage
  A's last `MoveNext` returns `false`. There is zero overlap.
- A task that yields `Break.It` completes **only that task**; the collection
  continues with the next one. Only `Break.AndStop` unwinds the whole
  collection/parent chain for that run.
- `Reset()` calls `Reset()` on each task's enumerator. Compiler-generated iterators do
  **not** support `Reset()` (it throws `NotSupportedException`); use custom enumerator
  classes if you need reuse via `Reset()`.
- `.Complete()` uses a thread-local `SyncRunner` per thread; it is not safe to share
  across threads concurrently.