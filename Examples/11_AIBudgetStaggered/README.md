# Example 11: AI Budget Staggered (StaggeredFlow)

## Scenario

You have **10 AI units** in a game. Each frame, every unit wants to "think" (run its
AI decision logic). But if all 10 think at once you get a frame spike. The solution is
to limit processing to **3 units per step** — a staggered update.

## Feature Demonstrated

`StaggeredFlow` — an `IFlowModifier` that caps the **number of tasks processed per
single `Step()` call** on a `SteppableRunner`.

## When / Why Use It

- You have many lightweight periodic tasks (AI ticks, sensor scans, UI refreshes) and
  want to spread them across frames instead of running all of them every frame.
- You need a deterministic, count-based budget (not time-based — see
  `TimeBoundFlow` for that).
- The runner is a `SteppableRunner`, so *you* control when each "frame" happens via
  `Step()`.

## How It Works

1. Create a `Svelto.Tasks.Lean.SteppableRunner`.
2. Call `runner.UseFlowModifier(new StaggeredFlow(3))` — at most 3 tasks will be
   processed per `Step()`.
3. Register N tasks with `Task().RunOn(runner)` — the extension method from
   `Svelto.Tasks.Lean.TaskRunnerExtensions`.
4. Each `runner.Step()` call processes up to `maxTasksPerIteration` tasks (those that
   haven't completed yet). The remaining tasks are deferred to subsequent steps.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `SteppableRunner` | `Svelto.Tasks.Lean` | Manual-step runner; you call `Step()` |
| `StaggeredFlow` | `Svelto.Tasks.FlowModifiers` | `IFlowModifier` that limits tasks per iteration |
| `UseFlowModifier()` | (on runner) | Swaps the flow strategy |
| `RunOn(runner)` | `Svelto.Tasks.Lean` | Extension that enqueues an `IEnumerator<TaskContract>` |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one iteration (come back next step) |

## Gotchas

- **StaggeredFlow limits tasks per step.** Extra tasks are *starved* until earlier ones
  complete. If every task yields every step (`yield return TaskContract.Yield.It`),
  the same first N tasks will run every step while later tasks may never get a turn.
- `TaskCollection`s count as a **single task** — staggering does not look inside a
  collection.
- The counter inside `StaggeredFlow` resets at the start of each `Step()` (via
  `Reset()` on the modifier), so the limit is per-step, not per-runner-lifetime.