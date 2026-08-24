# 03 · Loading Pipeline — `TaskContract` Return Values

## Scenario

A loading pipeline where a **child** task "downloads" and "parses" a config over
several steps, then **returns** the parsed config to the **parent** task via
`TaskContract`. The parent reads the returned value and prints it.

This is the foundation of composing tasks that produce results: the child does the
work, the parent consumes the output.

## Feature

**`TaskContract` return values** — a Lean task (`IEnumerator<TaskContract>`) can
`yield return <value>` to hand a value back to whoever continued it. The parent
reads it via `child.Current.ToInt()` / `.ToRef<T>()` / `.ToFloat()` etc.

The parent uses **`.Continue()`** so the child runs on the **same** runner and the
parent suspends until the child completes.

## When / Why to use it

- A loading sequence: download → parse → return config to caller.
- Any parent/child where the child computes a result the parent needs.
- You want the type-safe, allocation-aware return channel that `TaskContract`
  provides (primitives stored without boxing via a `[StructLayout(LayoutKind.Explicit)]`
  union; references stored as object).

## How it works

1. The parent task creates the child enumerator and keeps a reference to it.
2. The parent yields `child.Continue()`. The runner runs the child on the **same**
   runner and suspends the parent.
3. The child yields `TaskContract.Yield.It` a few times (simulating download
   progress) then `yield return configValue` — this sets `child.Current` to a
   `TaskContract` holding the value and finishes the child.
4. The parent resumes and reads `child.Current.ToRef<Config>()` (or `.ToInt()`
   for primitives).
5. The parent prints the config.

### The loading bar + config box

While the child is "downloading", a progress bar fills up. When done, a box is
drawn showing the parsed config contents.

## Key concepts

| Type / API | Purpose |
|---|---|
| `IEnumerator<TaskContract>` | Lean task shape. |
| `yield return <int/float/...>` | Return a primitive value (boxed into `TaskContract` via implicit operator). |
| `yield return <string>` / `TaskContract.FromReference(obj)` | Return a reference value. |
| `child.Continue()` | Run `child` on the same runner; parent waits. |
| `child.Current.ToInt()` | Unbox a returned `int` from the child's last `Current`. |
| `child.Current.ToRef<T>()` | Retrieve a returned reference as type `T`. |

## Gotchas

- **`yield return i` boxes the int** into a `TaskContract` (the implicit
  `operator TaskContract(int)` runs). You **must** use `.ToInt()` to read it
  back — `.ToRef<int>()` returns `null` because the int is stored in the value
  union, not the reference field.
- `.Continue()` runs the child on the **same** runner as the parent. Use
  `.RunOn(otherRunner)` only when you want the child on a *different* runner (then
  the parent yields the returned `Continuation`).
- You must **keep a reference to the child enumerator** (`var child = Child();`) so
  you can read `child.Current` after it completes. If you inline it
  (`yield return Child().Continue();`) you lose the handle.
- The value is available in `Current` **after** the child has completed and the
  parent has resumed. Reading it earlier returns the last *yielded* value, not the
  return value.
- Returning a reference type via `yield return someObject` works only if there is
  an implicit operator (`string` has one). For arbitrary objects use
  `yield return TaskContract.FromReference(obj)`.