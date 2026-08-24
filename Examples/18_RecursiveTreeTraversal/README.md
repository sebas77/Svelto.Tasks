# Example 18: Recursive Tree Traversal — Deep Continuation Chains

## Scenario

Walk a tree structure (a scene graph) using recursive `.Continue()` calls. Each child visit is a continuation that the parent waits for. The traversal is depth-first: enter a node, recurse into children, then exit.

## Feature

**Deep continuation chains** — recursive `.Continue()` calls that create arbitrarily deep parent-child task chains on the same runner.

`.Continue()` spawns a child task on the **same runner** as the parent and the parent suspends until the child completes. This is recursive — each level of the tree creates a deeper continuation chain.

## Why / When to Use

- **Scene graph traversal** — walk a hierarchy of game objects, processing each node as a task.
- **Nested async operations** — load a parent asset, then its children, then their children, where each level must wait for the level below.
- **Recursive algorithms** — any divide-and-conquer or tree-walk pattern where each level depends on results from deeper levels.
- **Deep task chains** — when you need 32+ levels of nested continuations.

## How It Works

```
              [ROOT]
             /      \
         [A]        [B]
         / \        / \
       [A1][A2]  [B1][B2]

Traversal (depth-first via .Continue()):
  ENTER ROOT
    → ENTER A           (ROOT waits via .Continue())
      → ENTER A1         (A waits via .Continue())
      ← EXIT A1
      → ENTER A2
      ← EXIT A2
    ← EXIT A
    → ENTER B
      → ENTER B1
      ← EXIT B1
      → ENTER B2
      ← EXIT B2
    ← EXIT B
  ← EXIT ROOT
```

1. `Traverse(node)` is an `IEnumerator<TaskContract>` that visits a node, then for each child does `yield return Traverse(child).Continue()`.
2. `.Continue()` spawns the child on the **same `SteppableRunner`** as the parent. The parent task suspends until the child completes.
3. This is recursive: each child call creates a deeper continuation chain.
4. The `SteppableRunner.Step()` ticks all tasks. The runner's internal list starts at capacity 3 — deeper trees trigger automatic resizes.

## Key Concepts

| Type | Namespace | Purpose |
|------|-----------|---------|
| `.Continue()` | `Svelto.Tasks` (extension) | Spawn child on same runner, parent waits |
| `SteppableRunner` | `Svelto.Tasks.Lean` | Manually-stepped runner |
| `.Step()` | — | Tick all tasks once |
| `TaskContract.Yield.It` | `Svelto.Tasks` | Yield one tick |
| `.RunOn(runner)` | `Svelto.Tasks.Lean` | Start a root task (returns `Continuation`) |

## .Continue() vs .RunOn() vs .Forget()

| Method | Returns | Parent waits? | Runs on | Use when |
|--------|---------|---------------|--------|----------|
| `.Continue()` | `TaskContract` | **Yes** | Same runner as parent | Child on same runner, parent must wait |
| `.RunOn(runner)` | `Continuation` | No (poll `.isRunning`) | Specified runner | Child on a different runner |
| `.Forget()` | `TaskContract` | **No** | Same runner (scheduled) | Fire-and-forget |

**Key:** `.Continue()` is the right choice for recursive same-runner traversal. The parent yields and waits; the child runs to completion; the parent resumes.

## Gotchas

- **Deep chains (32+ levels) work.** Tests confirm 32 nested `.Continue()` calls work correctly even when the runner's internal list resizes. The `SveltoTaskWrapper` struct is fully set before `SpawnContinuingTask` is called (which may trigger a resize), so the `this` reference stays valid.
- **With `SerialFlow`, a root task CANNOT wait for another root task.** Use `.Continue()` instead of `.RunOn(runner)` when the child should run on the same runner and the parent must wait. `.RunOn()` returns a `Continuation` handle you'd have to poll; `.Continue()` spawns inline and handles the wait for you.
- **`yield return TaskContract.Yield.It;` is required inside loops** to enable asynchronous execution. Forgetting it causes an infinite loop that blocks the runner.
- The runner's internal list starts at capacity `NUMBER_OF_INITIAL_COROUTINE = 3`. Resizes are handled safely — the wrapper is set before the spawn call.

## API Reference

```csharp
IEnumerator<TaskContract> Traverse(TreeNode node)
{
    // Process node...
    foreach (var child in node.Children)
    {
        yield return Traverse(child).Continue();  // parent waits for child
        // Parent resumes here after child completes
    }
    // Post-children processing...
}

// Run on a steppable runner:
var runner = new SteppableRunner("TreeRunner");
Traverse(root).RunOn(runner);
while (runner.hasTasks)
    runner.Step();
```