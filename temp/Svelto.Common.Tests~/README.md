# Svelto.Common.Tests

NUnit regression and contract tests for the non-Unity build of `Svelto.Common`.

## Coverage

The suite covers the public, non-Unity APIs compiled by `Svelto.Common.csproj`, including:

- Managed, native, shared, read-only, fixed-size, and thread-safe collections.
- Dictionary enumeration and structural-mutation contracts.
- Slot maps, sparse strategies, stale handles, generation retirement, and clear semantics.
- Ring buffers, bags, native arrays, streams, and memory utilities.
- Weak references/actions/events, context notification, and sequencing.
- Hashing, formatting, reflection, type caching, and helper types.
- Locks, concurrent collection operations, and thread utilities.
- Logging and the standard/no-op platform profilers.

Unity- and Burst-only code paths are intentionally outside this project. Debug-only safety checks are asserted in
Debug builds; Release tests assert only behavior that remains part of the optimized build.

The latest Debug coverage run reports **78.59% line coverage** and **68.06% branch coverage**. This includes source
that cannot be reached without Unity-specific compilation symbols, so the figures are not a percentage of only the
supported non-Unity surface.

## Running the suite

```pwsh
dotnet test Svelto.Common.Tests.csproj -c Debug
dotnet test Svelto.Common.Tests.csproj -c Release
dotnet test Svelto.Common.Tests.csproj -c Debug --collect:"XPlat Code Coverage"
```

Current verified results:

- Debug: 291 passed.
- Release: 281 passed. The difference is caused by tests for APIs that are themselves compiled only in Debug.
