# Svelto.Tasks Examples

21 self-contained console examples, one per main feature. Each folder is an independent project you can run with `dotnet run`.

## Running

```bash
cd Examples/01_GameLoop
dotnet run
```

Each example targets `net8.0` (rolls forward to newer SDKs if 8 isn't installed) and references Svelto.Tasks + Svelto.Common via project references — no NuGet packages needed.

## Examples

| # | Folder | Feature | Scenario |
|---|--------|---------|----------|
| 1 | [`01_GameLoop`](01_GameLoop/README.md) | Lean Task + SteppableRunner | Tick a runner from a game loop; task yields each frame |
| 2 | [`02_SimpleCoroutine`](02_SimpleCoroutine/README.md) | ExtraLean Task | Minimal-overhead countdown coroutine |
| 3 | [`03_LoadingPipeline`](03_LoadingPipeline/README.md) | TaskContract return values | Child returns parsed config to parent |
| 4 | [`04_ContinueChildTask`](04_ContinueChildTask/README.md) | Continue() | Parent delegates to child on same runner, waits |
| 5 | [`05_BackgroundComputation`](05_BackgroundComputation/README.md) | RunOn() + Continuation | Heavy math on background thread, poll from main |
| 6 | [`06_FireAndForgetLogging`](06_FireAndForgetLogging/README.md) | Forget() | Kick off telemetry task, don't wait |
| 7 | [`07_ReusableSpawnLoop`](07_ReusableSpawnLoop/README.md) | Break.It + pooling | Reusable spawn loop via `while(true) + Break.It` |
| 8 | [`08_CancellableChain`](08_CancellableChain/README.md) | Break.AndStop | If a step fails, abort the entire chain |
| 9 | [`09_OrderedLoading`](09_OrderedLoading/README.md) | SerialTaskCollection | download → parse → initialize, sequential |
| 10 | [`10_ConcurrentAnimations`](10_ConcurrentAnimations/README.md) | ParallelTaskCollection | Multiple tweens progressing together |
| 11 | [`11_AIBudgetStaggered`](11_AIBudgetStaggered/README.md) | StaggeredFlow | Limit N AI tasks per frame |
| 12 | [`12_FrameBudgetTimeBound`](12_FrameBudgetTimeBound/README.md) | TimeBoundFlow | Process tasks for at most 20ms per frame |
| 13 | [`13_BatchPathfinding`](13_BatchPathfinding/README.md) | ParallelJobCollection | 1000 units pathfind across 4 threads |
| 14 | [`14_ParallelDownloads`](14_ParallelDownloads/README.md) | MultiThreadedParallelTaskCollection | 4 downloads on 4 threads simultaneously |
| 15 | [`15_EntitySpawnPool`](15_EntitySpawnPool/README.md) | IteratorBlockPool | Reusable pooled iterator blocks for spawning |
| 16 | [`16_AsyncHttpAwaiter`](16_AsyncHttpAwaiter/README.md) | Awaiter interop | `await task.RunOn(runner)` |
| 17 | [`17_PauseMenu`](17_PauseMenu/README.md) | Pause/Resume | Freeze all game tasks when paused |
| 18 | [`18_RecursiveTreeTraversal`](18_RecursiveTreeTraversal/README.md) | Deep continuations | Walk a tree via recursive `.Continue()` |
| 19 | [`19_DelayedSpawn`](19_DelayedSpawn/README.md) | WaitForSecondsEnumerator | Spawn enemy 2s after start |
| 20 | [`20_CrossThreadSignal`](20_CrossThreadSignal/README.md) | WaitForSignal | Background thread signals main thread |
| 21 | [`21_StopRunnerNetTask`](21_StopRunnerNetTask/README.md) | TaskSynchronizationContext | Host .NET async methods on any Lean runner; dispose mid-flight → frozen + collected |

## See Also

- [`AGENTS.md`](../AGENTS.md) — how to use Svelto.Tasks and when (start here)
- [AI Guide: Svelto.Tasks](../Packages/com.sebaslab.svelto.tasks/.aiguides/AI_GUIDE_Svelto.Tasks.md) — full API reference
- [AI Guide: Svelto.Common](../Packages/com.sebaslab.svelto.common/.aiguides/AI_GUIDE_Svelto.Common.md) — data structures and utilities