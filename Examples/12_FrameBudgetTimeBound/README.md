# Example 12: Frame Budget Time-Bound (TimeBoundFlow)

## Scenario

You have **10 tasks** each taking **5 ms**. You want to cap total processing at
**20 ms per frame**. With `TimeBoundFlow(20f)`, roughly **4 tasks** will execute before
the budget runs out — the rest wait for the next step.

## Feature Demonstrated

`TimeBoundFlow` — an `IFlowModifier` that uses a `Stopwatch` to stop processing tasks
once the elapsed time exceeds a configurable millisecond threshold.

## When / Why Use It

- You have a mix of tasks with varying durations and want a **wall-clock budget** rather
  than a count-based limit.
- Common in game loops: "spend at most N ms on background work this frame."
- Unlike `StaggeredFlow` (which caps count), `TimeBoundFlow` adapts automatically when
  task durations vary.

## How It Works

1. Create a `Svelto.Tasks.Lean.SteppableRunner`.
2. Call `runner.UseFlowModifier(new TimeBoundFlow(20f))` — 20 ms budget per step.
3. Register tasks with `Task().RunOn(runner)`.
4. Each `runner.Step()`:
   - `TimeBoundFlow.Reset()` starts the internal `Stopwatch`.
   - Before each task is processed, `CanMoveNext` checks elapsed time. If it exceeds
     the budget, the runner stops iterating for this step.
5. Tasks that didn't get processed remain queued for the next `Step()`.

## Key Concepts

| Type | Namespace | Role |
|------|-----------|------|
| `SteppableRunner` | `Svelto.Tasks.Lean` | Manual-step runner |
| `TimeBoundFlow` | `Svelto.Tasks.FlowModifiers` | `IFlowModifier` with `Stopwatch`-based budget |
| `UseFlowModifier()` | (on runner) | Swaps the flow strategy |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one iteration |

## Gotchas

- **Starvation:** each tick restarts from the **first** task in the list. Tasks that never
  complete keep consuming the whole budget every tick, so tasks further down the list may
  never run (in this demo, tasks 5–10 execute zero times). Let tasks complete to free their
  slots, or use `StaggeredFlow`/`TimeSlicedFlow` when fairness across tasks matters more
  than a wall-clock budget.
- `TimeBoundFlow` bounds the **total tick duration**. If a single task itself exceeds
  the budget, it will still run to completion (the check happens *before* a task starts,
  not mid-task).
- `TaskCollection`s count as a **single task** — the time spent inside a collection
  counts against the budget as one unit, but the collection itself won't be interrupted
  mid-iteration.
- The `Stopwatch` is started fresh in `Reset()` at the beginning of each `Step()`, so
  budgets are per-step, not cumulative across steps.