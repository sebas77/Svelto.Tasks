# Svelto.Common - AI Developer Guide

> **Note:** Package-root `AGENTS.md` is the concise implementation entry point. This file is the authoritative API and behavior reference; update it first when behavior changes.

> **Scope disclaimer:** Document only the **public interface** of the library. Internal types (e.g., `DBC.Common.Check`, `SeqLock`) are out of scope: mention them only when they are externally observable through public behavior, such as exceptions thrown by public APIs.

> **Purpose:** A shared utility library used across all Svelto frameworks (ECS, Tasks, etc.). Provides high-performance data structures, memory management, logging, profiling, and platform abstractions. Designed to work in both Unity (with Burst/Jobs) and plain .NET.

## Architecture Overview

Svelto.Common is the foundation layer. Everything else in the Svelto ecosystem builds on it. The library is conditional-compiled: when used inside Unity, it leverages `Unity.Collections`, `Unity.Burst`, and Unity-specific APIs; outside Unity, it falls back to standard .NET equivalents.

Key design principles:
- **Dual-memory model:** Most data structures work with either managed (GC'd) or native (unmanaged) memory via strategy patterns.
- **Burst compatibility:** Many structs are designed to be usable inside Unity's Burst compiler (no managed references, no virtual calls).
- **Zero-cost abstractions:** Profilers, DBC checks, and debug sentinels compile to no-ops in release builds.
- **Struct-based keys:** Dictionaries require `struct` keys; use `RefWrapper<T>` to wrap class keys.

---

## Namespace Map

| Namespace | Contains |
|-----------|----------|
| `Svelto.Context` | Composition root, framework lifecycle |
| `Svelto` | `Console` (logging entry point) |
| `Svelto.Utilities` | Loggers, thread utilities, hash, delegates, time |
| `Svelto.Common` | Profiler, allocator, memory, type cache, shared static |
| `Svelto.Common.Internal` | Debug extensions |
| `Svelto.DataStructures` | All data structures (managed + unmanaged) |
| `Svelto.DataStructures.Native` | Native-memory dictionary/buffer variants |
| `DBC.Common` | Internal Design By Contract checks (not public API) |
| Global namespace | `FixedTypedArray4/8/16/32<T>`, `SpanList<T>`, `FastConcatUtility`, `StringBuilderUtils`, and weak action/event types |

---

## 1. Context (Framework Lifecycle)

### `ICompositionRoot`
The entry point interface for a Svelto application. Implement this on a class that bootstraps your application.
- `OnContextCreated<T>(T contextHolder)` — called when the context is created (e.g., Unity `Awake`).
- `OnContextInitialized<T>(T contextHolder)` — called after initialization (in `UnityContext<T>`, at the end of the first playing frame after `Start`).
- `OnContextDestroyed(bool hasBeenInitialised)` — called on teardown.

**When to use:** Implement this to bootstrap a Svelto application. In Unity, subclass `UnityContext<T>` (a `MonoBehaviour`) which calls these methods at the right lifecycle points.

### `IContextNotifer`
Coordinates framework lifecycle events. Objects register as listeners; the framework broadcasts initialization/destruction.
- `NotifyFrameworkInitialized()` / `NotifyFrameworkDeinitialized()` — broadcast to listeners.
- `AddFrameworkInitializationListener(IWaitForFrameworkInitialization)` — register for init notification.
- `AddFrameworkDestructionListener(IWaitForFrameworkDestruction)` — register for destruction notification.

**When to use:** Use `ContextNotifier` (the default impl) when you need to notify subsystems that the framework is ready or shutting down. Listeners are held weakly.

### `IWaitForFrameworkInitialization` / `IWaitForFrameworkDestruction`
- `OnFrameworkInitialized()` — called when framework is ready.
- `OnFrameworkDestroyed()` — called when framework is tearing down.

**When to use:** Implement these on objects that need to react to framework startup/shutdown.

### `ContextNotifier`
Default `IContextNotifer` implementation. Uses `WeakReference<T>` for listeners and notifies them in reverse registration order. Each notification phase can only happen once: its list is nulled afterward, and later registration for that phase throws.

### `UnityContext<T>` (Unity only)
Abstract `MonoBehaviour` that bridges Unity lifecycle to `ICompositionRoot`. `T` must be `class, ICompositionRoot, new()`.
- `Awake()` → `OnContextCreated`
- playing `Start()` + `WaitForEndOfFrame` → `OnContextInitialized`
- `OnDestroy()` → `OnContextDestroyed`

---

## 2. Logging

### `Svelto.Console` (static)
The unified logging entry point for ALL Svelto libraries. **Always use this instead of `UnityEngine.Debug` or `System.Console` in Svelto code.**
- `Log(string)` — informational log.
- `LogDebug(string)` — debug-only (`[Conditional("DEBUG")]`, compiled away in release).
- `LogWarning(string)` — warning (prefixed with `------> `).
- `LogError(string, Dictionary<string,string> extraData = null)` — error (prefixed with `-!!!!!!-> `).
- `LogException(Exception, string message = null, ...)` — exception with inner-exception tracing.
- `AddLogger<T>(T logger)` — register at most one logger for each concrete `T`; later registrations of the same type are ignored.
- `CompressLogsToZipAndShow(string zipName)` — asks every registered logger to export; support depends on the logger (`SimpleLogger` and `DefaultUnityLogger` do nothing).
- `logMessage` event — intercept all log calls externally.
- `onException` event — intercept all exceptions externally.

**How it works:** The static constructor auto-registers `DefaultUnityLogger` (Unity) or `SimpleLogger` (non-Unity). Additional loggers can be added via `AddLogger`. Each call fans out to all registered loggers.

### `ILogger`
Contract for log sinks.
- `Log(string, LogType, bool showLogStack, Exception, Dictionary<string,string>)` — the core log method.
- `OnLoggerAdded()` — called once when registered.
- `CompressLogsToZipAndShow(string)` — export logs.

### `LogType` enum
`Log`, `Exception`, `Warning`, `Error`, `LogDebug`

### Built-in loggers
| Logger | When | Purpose |
|--------|------|---------|
| `SimpleLogger` | Non-Unity default | Writes to `System.Console` |
| `DefaultUnityLogger` | Unity default | Formats with rich-text colors, forwards to Unity's log handler |
| `FasterUnityLogger` | Opt-in via `FasterLog.UseGlobally()` | Batched async file logger for player builds. Batches repeated messages by hash. |

### Unity logger setup helpers
- `Svelto.Console.DefaultLog.ReplaceUnityLogger(bool keepLogHandlerInEditor)` — replaces Unity's global log handler with a Svelto bridge.
- `Svelto.Console.FasterLog.UseGlobally(bool replaceUnityLogger)` — enables the high-performance file logger.

---

## 3. Profiling

### `IPlatformProfiler`
- `DisposableSampler Sample(string name)` — begin a named profiling scope (returns `IDisposable`).
- `DisposableSampler Sample<W>(W sampled)` — typed overload.

**Platform-profiler behavior:** Unless Unity 2018.3+, `ENABLE_PLATFORM_PROFILER`, and `DEBUG` are all active, `PlatformProfiler`, `PlatformProfilerMT`, `DisposableSampler`, and `PauseProfiler` are no-op structs. `StandardProfiler` remains an active `Stopwatch`-based profiler independently of those symbols.

### Implementations
| Type | Environment | Behavior |
|------|-------------|----------|
| `PlatformProfiler` | Unity + DEBUG + `ENABLE_PLATFORM_PROFILER` | Wraps `ProfilerMarker` |
| `PlatformProfilerMT` | Same, multi-threaded variant | Same, safe for worker threads |
| `PlatformProfiler` | Release/non-Unity | No-op |
| `StandardProfiler` | Non-Unity | Uses `Stopwatch`, logs elapsed on dispose |
| `StandardDisposableSamplerHolder` | Non-Unity | Programmatic access to `ElapsedMs`/`ElapsedNano` |

**When to use:** Wrap hot loops or methods in `using (profiler.Sample("name")) { ... }`. The platform profiler costs nothing in its disabled build; `StandardProfiler` always measures.

---

## 4. Memory Management

### `MemoryUtilities` (static)
Low-level native memory operations. Foundation for all unmanaged data structures.
- `NativeAlloc(uint bytes, Allocator, bool clear)` → `IntPtr`
- `NativeAlloc<T>(uint count, Allocator, bool clear)` → `IntPtr`
- `NativeRealloc(IntPtr, uint newBytes, Allocator, uint bytesToCopy, bool copy, bool memClear)` → `IntPtr`
- `NativeFree(IntPtr, Allocator)`
- `MemClear<T>(IntPtr, uint count)` / `MemClear(IntPtr, uint bytes)`
- `MemSet(IntPtr, uint bytes, byte value)`
- `MemMove<T>(IntPtr src, uint srcIndex, uint dstIndex, uint count)` — safe for overlapping memory
- `MemCpy<T>(IntPtr src, uint srcIndex, IntPtr dst, uint dstIndex, uint count)` — NOT safe for overlap
- `SizeOf<T>()` / `SizeOfAligned<T>()` — struct size with/without alignment padding
- `Align4(uint)` / `Pad4(uint)` — alignment utilities

**On Unity:** delegates to `UnsafeUtility` (Burst-compatible). **Off Unity:** uses `Marshal.AllocHGlobal`/`FreeHGlobal`.

### `Allocator` enum
Mirrors Unity allocator values where applicable:
| Value | Meaning |
|-------|---------|
| `Invalid` | Invalid |
| `None` | No allocation |
| `Temp` | Unity temporary allocator |
| `TempJob` | Unity temporary-job allocator |
| `Persistent` | Long-lived native allocation |
| `Managed` | Strategy marker; `MemoryUtilities.NativeAlloc` maps it to Unity `Persistent` |

Outside Unity, `NativeAlloc` uses `Marshal.AllocHGlobal` for every allocator value; `Temp` and `TempJob` have no automatic lifetime semantics there. Any successful native allocation must be released explicitly through the matching owner or `NativeFree`.

### `SharedStaticWrapper<T, Key>`
Cross-thread shared state. On Unity with Burst, uses `Unity.Burst.SharedStatic`. Off Unity, uses a static field.
- `ref T Data` — get/set the shared value.

**When to use:** Shared unmanaged state between main-thread code and Burst jobs. When Burst is enabled, initialize the shared value outside Burst before accessing it from Burst code.

---

## 5. Data Structures

### 5.1 Dynamic Arrays

#### `FasterList<T>`
The primary dynamic array. Faster than `List<T>` for most operations. Uses `uint` count internally.
- **Construction:** Multiple overloads (capacity, arrays, spans, other lists, collections).
- **Adding:** `Add(in T)` (returns `this` for fluent API), `AddAt(uint, in T)`, `AddRange(...)`, `GetOrCreate(uint, in Func<T>)` → `ref T`.
- **Removing:** `RemoveAt(uint)` (shifts), `UnorderedRemoveAt(uint)` (O(1) swap-remove, order not preserved).
- **Access:** `this[int]` / `this[uint]` with `ref` returns. Bounds-checked in DEBUG.
- **Count management:** `SetCountTo(uint)`, `EnsureCountIsAtLeast(uint)`, `IncrementCountBy(uint)`, `TrimCount(uint)`.
- **Capacity:** `IncreaseCapacityBy(uint)`, `IncreaseCapacityTo(uint)`, `Trim()`.
- **Clearing:** `Clear()` (type-aware: zeroes managed types, resets count), `MemClear()` (zeroes entire buffer), `ResetToReuse()` (sets count to 0 without clearing).
- **Stack ops:** `Push(in T)` → `uint`, `Pop()` → `ref readonly T`, `Peek()` → `ref readonly T`.
- **Enumeration:** `GetEnumerator()` → `FasterListEnumerator<T>` (ref-returning, modification-checked in DEBUG).

**When to use:** Anywhere you'd use `List<T>`. Use `UnorderedRemoveAt` for O(1) removal when order doesn't matter.

#### `FasterReadOnlyList<T>` (readonly struct)
Non-owning view of a `FasterList<T>`. The wrapper cannot replace the list reference, but its indexers return mutable `ref T`; it does not enforce element immutability. Implicit conversion from `FasterList<T>`.

#### `LocalFasterReadOnlyList<T>` (readonly ref struct)
A count snapshot holding the list's existing `T[]` and current count, without retaining the `FasterList<T>` object. It does not copy the elements, and its indexers return mutable `ref T`, so later writes to the backing array remain visible. The managed array makes this unsuitable for Burst jobs. Implicit conversion from `FasterList<T>` and, transitively, from `FasterReadOnlyList<T>`.

#### `FasterListPool<T>` (static)
Thread-local pool of `FasterList<T>` instances. `Get()` / `Release(FasterList<T>)`; release calls `Clear()` and preserves capacity.

#### `FasterListEnumerator<T>` (ref struct)
Ref-returning enumerator. Detects modification during iteration in DEBUG.

#### `TombstoneList<T>`
A list with O(1) removal that leaves "tombstones" in removed slots, which are reused for future additions. Returns a generation-aware `TombstoneHandle` on add.
- `Add(in T)` → `TombstoneHandle`
- `AddByRef(out TombstoneHandle)` → `ref T`
- `RemoveAt(TombstoneHandle)` — marks slot as tombstone, links to free list
- `this[TombstoneHandle]` → `ref T`

**When to use:** When callers need handles that remain valid across unrelated additions and removals. A handle becomes invalid when its item is removed, its slot is reused, or the list is cleared. `Has(handle)` checks that the exact slot generation is still live. Add and remove are O(1).

#### `TombstoneHandle` (readonly struct)
Index-and-generation handle into `TombstoneList<T>`. `Invalid` and a manually constructed `new TombstoneHandle(index)` are invalid; only generated handles carry a usable generation. `IsInvalid` rejects malformed handles, while `TombstoneList<T>.Has` checks whether the same index/generation is live in that list. Handles contain no owner identity, so do not pass them between lists: independently created lists can issue equal handles.

### 5.2 Dictionaries

#### `ISveltoDictionary<TKey, TValue>` / `IReadOnlySveltoDictionary<TKey, TValue>`
The dictionary contracts.
- **Read-only:** `count`, `ContainsKey`, `TryGetValue`, `TryFindIndex(TKey, out uint)`, `GetIndex(TKey)`, `Dispose`.
- **Full:** `Add`, `Set`, `Clear`, `GetOrAdd` (multiple overloads), `GetDirectValueByRef(uint)`, `GetValueByRef(TKey)`, `EnsureCapacity`, `IncreaseCapacityBy`, `Remove`, `Trim`, `this[TKey]`.

**Key insight:** `TryFindIndex`/`GetIndex` expose the internal value-array index, enabling direct `ref` access for maximum performance. That index is not a stable handle: removal swap-moves the last entry, and resizing or later mutation can invalidate assumptions about retained references.

#### `SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy>`
The core implementation. Values are stored contiguously, enabling direct array iteration. Uses three separate arrays: `_valuesInfo` (keys+hashes+chains), `_values` (values), `_buckets` (hash buckets). Uses Daniel Lemire's `FastMod` on 64-bit.

**Important:** Not thread-safe. For thread-safe use, wrap with `ThreadSafeDictionary`. Requires `struct` keys implementing `IEquatable<T>`.

#### `FasterDictionary<TKey, TValue>`
The managed-memory wrapper around `SveltoDictionary`. **This is the primary dictionary for most use cases.**
- Requires `TKey : struct, IEquatable<TKey>`.
- **For class keys (e.g., `string`):** wrap in `RefWrapper<T>` or `RefWrapperString`.
- Additional: `Recycle()` (reset keeping value arrays), `CopyValuesTo(...)`, `Exclude/Intersect/Union/CopyFrom` (set operations), `GetValues(out uint)` → `MB<TValue>`.

#### Native dictionary variants (`Svelto.DataStructures.Native`)
| Type | Purpose |
|------|---------|
| `SveltoDictionaryNative<TKey, TValue>` | Native-memory dictionary for Jobs/Burst |
| `SharedSveltoDictionaryNative<TKey, TValue>` | Dictionary stored in native memory, shareable across threads |
| `ReadonlySveltoDictionaryNative<TKey, TValue>` | Read-only wrapper |
| `ReadonlySharedSveltoDictionaryNative<TKey, TValue>` | Read-only wrapper for shared dict |
| `LocalSveltoDictionaryNative<TKey, TValue>` (ref struct) | Non-owning local handle, can't be stored as field |
| `LocalReadonlySveltoDictionaryNative<TKey, TValue>` (ref struct) | Read-only local handle |

`SharedSveltoDictionaryNative` places the dictionary struct itself in native memory so value-type copies alias the same state. “Shared” does not add locking or make concurrent mutation safe. Treat copied native owners as aliases and dispose the allocation exactly once after all users stop.

#### `ThreadSafeDictionary<TKey, TValue>`
Wraps `FasterDictionary` with `ReaderWriterLockSlimEx`. All reads use read-lock, writes use write-lock, `GetOrAdd` uses upgradable read.

**When to use:** Multi-threaded dictionary access. Note: `GetDirectValueByRef`/`GetValueByRef` throw `NotSupportedException` (too unsafe for multi-threading).

#### `SveltoDictionaryNode<TKey>`
Internal node in the dictionary's collision chain: `hashcode`, `previous` (chain link), `key`.

### 5.3 Buffer Strategies (Dual Memory Model)

#### `IBufferStrategy<T>`
Abstracts managed vs. native memory for data structures. Through this interface, dictionaries and other structures can use either memory type interchangeably.
- `Alloc(uint size, Allocator, bool memClear)`, `Resize(uint, bool copy, bool memClear)`.
- `ShiftRight(uint index, uint count)`, `ShiftLeft(uint index, uint count)`.
- `ref T this[uint/int]` — ref-returning indexer.
- `AsBytesPointer()`, `SerialiseFrom(IntPtr)`, `ToBuffer()`, `Dispose()`.

#### `ManagedStrategy<T>`
Managed-array-backed strategy. Uses `MBInternal<T>`. `AsBytesPointer`/`SerialiseFrom` throw `NotImplementedException`. `Dispose` is a no-op (GC handles it).

#### `NativeStrategy<T>` (in `Svelto.DataStructures.Native`)
Native-memory-backed strategy. Uses `NBInternal<T>` + `MemoryUtilities`. Intended for Burst/Jobs. Its public constraint is `T : struct`, but Debug rejects types containing managed references; callers must supply an unmanaged-compatible type in every build. `AsBytesPointer()` and `SerialiseFrom()` currently throw `NotImplementedException`, just as they do on `ManagedStrategy<T>`.

#### `IBuffer<T>` / `IBufferBase`
Base buffer interface: `CopyTo`, `Clear`, `capacity`, `isValid`.

#### `NB<T>` (Native Buffer, struct)
Wrapper around a native array pointer. Not meant to resize or be freed. Cannot have a count (it's a fixed view).
- `this[uint/int]` → `ref T` (via `Unsafe.AsRef`).
- `AsReader()` → `NB<T>.Reader` (read-only, `ref readonly T`).
- `AsWriter()` → `NB<T>.Writer` (write, `ref T`).
- `Create(IntPtr array, int capacity, IntPtr rwState)` — static factory.

**When to use:** Inside Unity Jobs/Burst to wrap external native arrays. `AsReader`/`AsWriter` provide Debug-only misuse detection, not synchronization; the caller must still arrange safe concurrent access.

#### `MB<T>` (Managed Buffer, ref struct)
Wrapper around a managed `T[]` array. Same pattern as `NB<T>` but for managed memory.
- `this[uint/int]` → `ref T`.
- `AsReader()` / `AsWriter()` — lifetime guards only; unlike `NB<T>.Reader`, `MB<T>.Reader` currently returns mutable `ref T`.
- `ToManagedArray()` → `T[]`.
- `Set(T[])`, `CopyFrom(T[], uint)`.

### 5.4 Fixed-Size Arrays

#### `FixedTypedArray4/8/16/32<T>`
Fixed-size arrays embedded in structs (no heap allocation). `T : unmanaged`.
- `capacity` → 4/8/16/32.
- `this[int]` → `T` (get/set). Callers must supply `0 <= index < capacity`; the Debug DBC check only verifies the upper bound and is compiled out in Release.

These types are in the global namespace and are available only under `NEW_C_SHARP || !UNITY_5_3_OR_NEWER`.

**When to use:** In unmanaged/Burst contexts where regular arrays can't be used. Fields are inline, accessed via `Unsafe.Add`.

#### `SpanList<T>` (ref struct)
A global-namespace list over a caller-owned `Span<T>` with a count. `Add(T)` and `AddRange(Span<T>)` throw if the fixed capacity would be exceeded; the ref indexer validates against the current count. `AsSpan()` returns the used portion.

### 5.5 Ring Buffers / Queues

#### `CircularQueue<T>`
Fixed-capacity circular queue. Throws on overflow (does NOT overwrite).
- `Enqueue(in T)` (throws if full), `Dequeue()` → `ref T` (throws if empty).
- `CopyTo(Span<T>)`, `GetEnumerator()`.

#### `RingBuffer<T>`
True ring buffer with overwrite-on-full semantics. Overwrites oldest element when full.
- `Enqueue(in T)` — overwrites oldest if full.
- `Dequeue()` → `ref T` (throws if empty).
- Uses `_count` to distinguish full from empty.

**When to use:** Sliding-window/log-buffer scenarios where newest data matters most.

#### `UnmanagedCircularQueue<TCell>`
Non-concurrent, managed-array-backed ring buffer with an unmanaged cell type. `TCell : unmanaged`. Uses monotonic `long head`/`tail` with a bitmask.
- `TryEnqueue(in TCell)` / `TryDequeue(out TCell)` — non-throwing.
- Supports copying partial bytes via `count` parameter.

#### `UnmanagedConcurrentCircularQueue<TCell>`
Bounded, managed-array-backed MPMC lock-free ring queue (Vyukov algorithm). `TCell : unmanaged`.
- Uses CAS on `head`/`tail` with sequence numbers per slot.
- `PaddedLong` for `head`/`tail` (64-byte padding to avoid false sharing).
- `TryEnqueue` / `TryDequeue` — non-blocking, returns false if full/empty.

**When to use:** High-performance multi-producer multi-consumer queue. Lock-free, no `lock` statements.

### 5.6 Slot Maps

#### `ManagedSlotMap<T>`
A managed-memory slot map. Normal use obtains keys from `Add`, which returns a versioned `ValueIndex` handle. `Has` can detect stale handles from removed items.
- `Add(T)` → `ValueIndex`
- `Remove(ValueIndex)` — increments version, links to free list
- `this[ValueIndex]` → `T` (validates version)
- `Has(ValueIndex)` → `bool`
- O(1) add, remove, lookup.

Indexer and `Remove` validation use Debug-only DBC checks. In Release, call `Has(handle)` before using any handle whose validity is uncertain; stale access can otherwise read or remove the current occupant of a reused slot.

**Lifecycle:** `ManagedSlotMap<T>` exposes `Dispose()` but does not implement `IDisposable`; call it explicitly when the map is no longer needed.

**When to use:** When you need O(1) add/remove with stable, versioned handles. Especially useful when external code holds references that may become stale.

#### `ValueIndex` (readonly struct)
External handle: lower 24 bits = sparse index, upper 8 bits = version. It has a public `(uint index, byte version)` constructor and no owner identity, so treat only values returned by the target map as handles. A slot is permanently retired after generation 255 rather than allowing an old handle to become valid again.

#### `SparseIndex`
Internal per-slot metadata: dense index, version, free-list links.

### 5.7 Monotonic Window Buffer

#### `MonotonicWindowBuffer<T>`
SPSC (single-producer/single-consumer) fixed-capacity sliding window indexed by a monotonically increasing `int` "logical index." Producer may publish indices out-of-order (e.g., set 8 before 5).
- `SetHead(int newHead)` — **must be called before any `Add`**. Sets the starting logical index. Cannot move backwards.
- `Add(int index, in T value)` → `bool` — publisher writes value then marker (release). Throws if outside window or head not set; returns `false` for retired (below-head) indices. Re-adding an already-published index is a silent no-op that returns `true` without updating the value.
- `TryGet(int index, out T value)` → `MonotonicSlotState` — peek without retiring; throws `MonotonicWindowBufferOverflowException` for indices above the window.
- `TryPeek(out T value)` → `bool` — peek at head if published; throws `InvalidOperationException` if the head was never set.
- `TryDequeue(out T value)` → `bool` — dequeue (retire) head if published; throws `InvalidOperationException` if the head was never set.
- `Count` — **WARNING:** counts holes (unpublished indices). NOT the number of dequeue-able items. Do NOT use `if (Count > 0) TryDequeue()`.
- `HighestPublishedIndex` — highest index published so far.

**Important:** T must be immutable after `Add` or thread-safe for concurrent producer write + consumer read. Consumer consuming strictly in-order preserves queue semantics. Holes stall the consumer.

#### `MonotonicSlotState` enum
`NotPublished`, `Published`, `Consumed`, `OutOfRange`, `NotInitialised` (head not set).

### 5.8 Streams

#### `SveltoStream`
Core cursor-based reader/writer over caller-provided memory (a linear cursor, not a ring buffer; it owns no memory). Burst-compatible.
- `Read<T>(in Span<byte>)` → `ref T`, `Write<T>(in Span<byte>, in T)`.
- `WriteSpan<T>` / `ReadSpan<T>` — length-prefixed span read/write; the prefix is a `ushort` byte count, so spans whose serialized size exceeds 65,535 bytes silently truncate on write.
- `OverwriteAt<T>` — overwrite previously written data.
- `Clear()` / `Reset()` — both reset only the read/write cursor; `length` and buffer contents are kept.
- `CanAdvance()` reports at least one byte free; `CanAdvance(int)` / `CanAdvance<T>()` return `false` when *exactly* the requested size remains (strict `<` against capacity), even though `Write` accepts the exact fit. Leave one byte of headroom when gating writes with them.

#### `ManagedStream`
Wraps a `byte[]` (or `ArraySegment<byte>`) with `SveltoStream`. Read/write typed values and spans over a managed array. `UnsafeRead<T>` allows reading fewer bytes than `sizeof(T)` into a struct (optional destination offset); there is no matching `UnsafeWrite`. `stream.ReadByteArraySegment<T>()` reads a span written by `WriteSpan` as a zero-copy `ByteArraySegment<T>` over the buffer.

#### `UnmanagedStream`
Wraps a `byte*` with `SveltoStream`. Read/write typed values over native memory; supports `UnsafeWrite<T>` (partial-size write), but has no `UnsafeRead`.

#### `ByteArraySegment<T>`
Wraps either `Memory<byte>` or `T[]` as a readable segment whose `Span` is mutable. Implicit conversion to `ReadOnlySpan<T>`. `T : unmanaged`.

**When to use streams:** Type-safe serialization over raw memory buffers. Useful for network serialization, save data, etc.

### 5.9 Unmanaged/Native Structures

#### `NativeBag`
Heterogeneous native-memory queue. Can enqueue/dequeue different struct types (must dequeue in order); callers must preserve the enqueue/dequeue type sequence. Burst-compatible.
- `Enqueue<T>(in T)`, `Dequeue<T>()` → `T` — `T : struct` (not `unmanaged`) because of Svelto.ECS constraints; no type or sequence validation is performed, so storing managed-containing types is unsafe and undetected.
- `ReserveEnqueue<T>(out UnsafeArrayIndex)` → `ref T` — reserve space, update later.
- `AccessReserved<T>(UnsafeArrayIndex)` → `ref T`.
- `count`, `capacity`, `IsEmpty()`, `Clear()`.

**When to use:** A growing native queue or a preallocated native-memory pool for struct values. `count` is bytes, not item count. Like all native-owning structs, copies alias the same allocation: dispose exactly one copy.

#### `NativeDynamicArray`
Type-erased dynamic array over native memory. Type specified per-method-call via generic parameter.
- `Alloc<T>(uint length)` / `Alloc<T>(Allocator, uint)` — static factories.
- `Get<T>(uint)` → `ref T`, `Set<T>(uint, in T)`, `Add<T>(in T)`.
- `Resize<T>(uint)`, `RemoveAt<T>(uint)`, `UnorderedRemoveAt<T>(uint)`.
- `ToManagedArray<T>()` → `T[]`.
- In DEBUG: type consistency check via hash.

#### `NativeDynamicArrayCast<T>`
Typed wrapper around `NativeDynamicArray` that fixes the type parameter. Cleaner API: `this[int]` → `ref T`, `Add(in T)`, etc.

#### `AtomicNativeBags` (Unity only, `UNITY_NATIVE`)
Collection of `NativeBag` instances sized `JobsUtility.MaxJobThreadCount + 1` (one per worker thread plus the main thread). Each thread writes to its own bag without synchronization.

#### `SharedNativeInt`
A native `int` shared across threads/jobs. Atomic `Increment`/`Decrement`/`Add`/`CompareExchange` via `Interlocked`.

#### `SharedDisposableNative<T>`
Stores an unmanaged `IDisposable` struct in native memory (`Allocator.Persistent`). `Dispose()` calls `value.Dispose()` then frees native memory. Copies alias the same allocation; dispose exactly one copy. `value` is the `ref T` accessor (Debug-guarded against use after dispose).

### 5.10 Thread-Safe Wrappers

#### `ThreadSafeFasterList<T>`
Wraps `FasterList<T>` with `ReaderWriterLockSlimEx`. Reads use read-lock, writes use write-lock. **Caveats:** the ref-returning indexer takes the lock only while fetching the reference — the returned `ref T` is used after the lock is released, so it is not safe against concurrent mutation; `GetEnumerator()` enumerates the underlying list without holding any lock. Prefer copy-out (`ToArrayFast`) patterns in contested code.

#### `ThreadSafeStack<T>`
Wraps `System.Collections.Generic.Stack<T>` with a plain `lock` (not `ReaderWriterLockSlimEx`). `GetValues` returns a `ThreadSafeValues` guard struct that holds the monitor for the enumeration lifetime — always use it with `using` and dispose it promptly.

### 5.11 Helpers

#### `RefWrapper<T>` (readonly struct)
Wraps a reference type (`class`) as a `struct` for use as a dictionary key. Caches hash at construction.
- Implicit conversions both ways.
- `RefWrapper<T, Comparer>` — with custom `IEqualityComparer<T>`.

**When to use:** `FasterDictionary`/`SveltoDictionary` require `struct` keys. To use a `string` or other class as key, wrap it: `new RefWrapper<string>("myKey")`.

#### `RefWrapperString` (readonly struct)
Wraps `string` as a struct. Implicit conversions both ways. Specifically for string dictionary keys.

#### `RefWrapperType` (readonly struct)
Wraps `System.Type` as a struct. `NativeRefWrapperType` maps it to a stable-per-process `Guid` for Burst compatibility; the GUID cache is a static `FasterDictionary` built without locking, so first-time construction of a given wrapper is not safe to race across threads.

#### `HashHelpers` (static)
Daniel Lemire's `FastMod` for fast modulo. Prime number tables for hash table sizing.

#### `TypeCache<T>` (static)
Caches `type` (`Type`), `name`, `fullName`, and `isUnmanaged` for type `T` (all public lowercase fields). Companion: `TypeHash<T>.hash` — Burst-safe type hash used for native-structure type checks.

### 5.12 Sentinel (Debug Thread Safety)

#### `Sentinel`
Debug-only thread safety validation for `NB`/`MB` buffers. In release, zero-cost.
- Writer: requires state==0, sets to -1 via CAS.
- Reader: requires state != -1, increments via CAS.
- Used via `buffer.AsReader()` / `buffer.AsWriter()` which create `TestThreadSafety` guards.

---

## 6. Utilities

> `DBC.Common` (Design By Contract) is internal infrastructure, not public API: every Svelto library asserts through its own internal DBC copy, and nothing else should be used for assertions inside Svelto code. What callers observe from the outside: in Debug builds, contract violations surface as the public `DBC.Common` exception types (`PreconditionException`, `PostconditionException`, `InvariantException`, `AssertionException`); in Release the checks are compiled away (zero-cost).

### `ThreadUtility` (static)
Thread synchronization utilities (spinning, yielding, sleeping).
- `Yield()` — `Thread.Yield` (give core to another thread on same core).
- `TakeItEasy()` — `Thread.Sleep(1)` (force context switch).
- `Relax()` — `Thread.Sleep(0)` (may context switch).
- `Spin()` — `Thread.SpinWait(4)`.
- `Wait(ref int iterations, int frequency)` — yield every N iterations.
- `SleepWithOneEyeOpen(float ms, in Stopwatch, SyncStrategy, int frequency)` — spin-wait with periodic yields.

### `Murmur3` (static)
Fast, evenly-distributed `uint` hash from `byte[]`. Seed `0x1337`.

### `FastConcatUtility` (static)
Thread-local `StringBuilder` for allocation-free string concatenation. Extension methods on `string`: `FastConcat(int)`, `FastConcat(string)`, etc.

### `StringBuilderUtils` (static, global namespace)
Formatting helpers with Unity rich-text color tags. The numeric overloads (`AppendWithColor`/`AppendValue` for `int`, `long`, `float` with fixed decimals, `DateTime` as `HH:mm:ss`) append without `ToString` allocations; the `string` overload allocates via concatenation.

### `ReaderWriterLockSlimEx` (struct)
Wraps `ReaderWriterLockSlim` with `NoRecursion`. On WebGL: all methods are no-ops.

### `WeakAction` / `WeakEvent`
- `WeakAction` — holds an `Action`'s target as a weak reference (static delegates stay strong). `IsAlive`, `Invoke()`.
- `WeakEvent` — multicast holder with `+`/`-` operators (not a C# `event`) that doesn't prevent GC of subscribers; dead handlers are pruned on `Invoke`.

**When to use:** When subscribers may forget to unsubscribe. Prevents memory leaks from event handler references.

### `WeakReference<T>` (struct)
Struct wrapper around `System.WeakReference`. Can be used as dictionary key or in struct-based data structures. `T : class`.

### `Sequence<T, En>` / `ISequenceOrder` / `SequencedAttribute`
Orders a collection of objects by a predefined sequence.
1. Define a `struct : ISequenceOrder` with `enginesOrder` string array.
2. Tag items with `[Sequenced("name")]`.
3. `new Sequence<T, En>(items)` sorts items to match.

### Delegates
- `ActionRef<T>(ref T)` — action with one ref parameter.
- `ActionRef<T, W>(ref T, ref W)` — two ref parameters.
- `ActionIn<T>(in T)` — action with in parameter.
- `FuncRef<T, W>(ref T) → W` — function with ref parameter.
- `FuncIn<T, W>(in T) → W` — function with in parameter.

### `PlayerLoopUtility` (Unity only)
Insert custom update functions into Unity's player loop with precise ordering.
- `AddSystemBefore<TExisting, TCustom>`, `AddSystemAfter<TExisting, TCustom>`.
- `AddSystemAsFirstChild<TParent, TCustom>`, `AddSystemAsLastChild<TParent, TCustom>`.
- `RemoveSystem<TCustom>`, `ClearAllCustomSystems`.

### `Utils.NextPowerOfTwo(int/uint)`
Returns the smallest power of two >= input, with a floor of 2 (`NextPowerOfTwo(0)` and `NextPowerOfTwo(1)` both return 2).

### `TypeToString<T>` / `TypeToString`
Caches `typeof(T).ToString()` in a static field for fast type name lookup.

### Other public utilities
- `TimeUtils.ToNanoseconds(this TimeSpan)` — ticks × 100 conversion (global namespace).
- `DataToString.DetailString(Dictionary<string,string>)` — formats extra log data with teal color tags.
- `NetFXCoreWrappers` — reflection compatibility extensions (`GetInterfacesEx`, `IsValueTypeEx`, `GetCustomAttributes`, …) for platforms with limited reflection (global namespace).
- `FastInvoke<T>` — builds an unboxed field setter for struct fields holding an interface reference.
- `DebugExtensions.TypeName<T>()` (`Svelto.Common.Internal`) — cached type-name lookup used by profilers.

---

## Quick Reference: Choosing a Data Structure

| Need | Use |
|------|-----|
| Dynamic array | `FasterList<T>` |
| Non-owning list view (mutable refs, no copies) | `FasterReadOnlyList<T>` or `LocalFasterReadOnlyList<T>` (ref struct) |
| Slot handles invalidated on removal/reuse | `TombstoneList<T>` |
| Dictionary (managed) | `FasterDictionary<TKey, TValue>` |
| Dictionary (native/Burst) | `SveltoDictionaryNative<TKey, TValue>` |
| Dictionary with synchronized multi-thread access | `ThreadSafeDictionary` |
| Aliased native dictionary state (caller provides synchronization) | `SharedSveltoDictionaryNative` |
| Dictionary with class keys | `FasterDictionary<RefWrapper<string>, TValue>` |
| Fixed queue (throws on full) | `CircularQueue<T>` |
| Ring buffer (overwrites oldest) | `RingBuffer<T>` |
| Lock-free MPMC queue | `UnmanagedConcurrentCircularQueue<TCell>` |
| Slot map (versioned handles) | `ManagedSlotMap<T>` |
| Monotonic window buffer | `MonotonicWindowBuffer<T>` |
| Heterogeneous native queue | `NativeBag` |
| Native dynamic array | `NativeDynamicArray` / `NativeDynamicArrayCast<T>` |
| Serialization | `ManagedStream` / `UnmanagedStream` |
| Fixed-size struct array | `FixedTypedArray4/8/16/32<T>` |
| Thread-safe list | `ThreadSafeFasterList<T>` |
| Per-thread bags (jobs) | `AtomicNativeBags` |
| Shared atomic int | `SharedNativeInt` |

---

## Practical Patterns & Gotchas (from tests)

### Universal Dispose Pattern
Every native/unmanaged structure must be disposed in `try/finally`:
```csharp
var bag = new NativeBag(Allocator.Persistent);
try { /* use */ } finally { bag.Dispose(); }
```
This applies to: `NativeBag`, `NativeDynamicArray`, `SharedNativeInt`, `SharedDisposableNative<T>`, `SveltoDictionaryNative`, and all native strategy-backed structures.

### FasterList
- `AddAt(index, value)` can create **gaps** — indices between old count and index are `default`.
- `UnorderedRemoveAt(i)` returns `bool`: `true` if a swap occurred (item was not last), `false` if it was the last element (no swap, slot just cleared).
- `TrimCount(n)` only changes the logical `count`, not the capacity or buffer.
- `GetOrCreate(index, factory)` — factory is called **only if** the slot is `default(T)`. Returns `ref T` for in-place mutation.
- `ToSpan<T>()` / `ToByteSpan<T>()` require `#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER` (not available in older Unity).
- `CopyFrom` extension overloads accept `T[]`, `IList<T>`, `FasterList<T>`, and `FasterReadOnlyList<T>`, but are constrained to `T : unmanaged`.
- Implicit conversions chain: `FasterList<T>` → `FasterReadOnlyList<T>` → `LocalFasterReadOnlyList<T>`.

### TombstoneList
- **Handle reuse**: After `RemoveAt(h)`, the next `Add` can reuse the same slot index, but it returns a new generation-aware handle. The old `h` remains invalid.
- **Invalidation**: `RemoveAt`, slot reuse, and `Clear()` invalidate previous handles. Use `Has(h)` before retaining or dereferencing an externally held handle.
- **Invalid handle access**: `TombstoneHandle.Invalid`, manually constructed bare-index handles, removed handles, and stale handles throw `PreconditionException` when used for access or removal.
- **Enumerator invalidation**: In Debug builds, Add/Remove during enumeration is detected on the next `MoveNext()` and throws `InvalidOperationException`; the version check is absent in Release.
- `AddByRef(out handle)` returns `ref T` for in-place initialization.
- Constructor accepts initial capacity: `new TombstoneList<int>(initialSize)`.

### SveltoDictionary / FasterDictionary
- `Add` rejects an existing key and `Set` rejects a missing key only in Debug contract builds. In Release those checks are compiled out: `Add` overwrites an existing value and `Set` can insert a missing key. Use `TryAdd`, `ContainsKey`, or the indexer when behavior must not depend on build configuration.
- `GetIndex` and `GetValueByRef` throw for a missing key only in Debug contract builds. In Release a missing lookup falls through with index `0`; validate with `TryFindIndex` when absence is possible.
- `RecycleOrAdd<TConcrete>(key, builder, recycler)` is a **polymorphic recycling** API:
  - If key is **missing**: `builder` is called, value stored, `recycler` NOT called.
  - If key **exists**: **neither** builder nor recycler is called — existing value returned as-is via `ref`.
  - `TConcrete` is a subclass of `TValue`; enables type-safe recycling of pooled objects.
- `Recycle()` resets count and buckets but keeps value arrays (for object reuse without reallocation).
- The full `SveltoDictionary<...>` generic is verbose (5 type parameters). Use `FasterDictionary<K,V>` for the common managed case.
- Not thread-safe. Use `ThreadSafeDictionary` for multi-threaded access.
- `GetDirectValueByRef` / `GetValueByRef` throw `NotSupportedException` on `ThreadSafeDictionary` (too unsafe for multi-threading).

### RingBuffer vs CircularQueue
- **RingBuffer** = overwriting (drops oldest when full, `count` stays at capacity).
- **CircularQueue** = throwing (throws when full).
- `CircularQueue` capacity 4 only holds **3** elements (one slot kept empty to distinguish full/empty).
- `RingBuffer` enumerator snapshots the starting cursor and item count, but not the backing values. Later writes—especially overwrite-on-full writes—can change values observed by an existing enumerator.
- `UnmanagedCircularQueue` / `UnmanagedConcurrentCircularQueue` use `Try*` pattern (return `bool`, no exceptions).
- The **size overload** `TryEnqueue(value, byteCount)` allows partial writes (e.g., write only 4 bytes of an 8-byte struct). The caller must ensure `0 <= byteCount <= sizeof(TCell)`; this is only a `Debug.Assert`, and invalid values can corrupt memory in Release.

### MonotonicWindowBuffer
- **Head is monotonic** — `SetHead` can only move forward; backward throws `InvalidOperationException`.
- **Must call `SetHead` before any `Add`** — `Add` throws `MonotonicWindowBufferOverflowException` if head is -1.
- `Add` returns `false` (not throws) for indices **below** head, but **throws** for indices **above the window**.
- `Count` includes gaps — it's the span from head to highest published, NOT the number of published items. Do NOT use `if (Count > 0) TryDequeue()`.
- `TryGet` returns `MonotonicSlotState` enum, not `bool`.
- `TryPeek`/`TryDequeue` throw `InvalidOperationException` if `SetHead` was never called; with the head set, they return `false` (not throw) while the head slot is still unpublished.
- `TryDequeue` does not clear the retired slot — for reference types, the value stays referenced until the slot is overwritten.

### Streams (SveltoStream, ManagedStream, UnmanagedStream)
- `SveltoStream` requires an **external buffer** passed as `Span<byte>` to every operation — it doesn't own memory.
- **Must call `Reset()` between writing and reading** — the cursor is shared.
- `ManagedStream.AsSpan()` returns only the **written** portion, not the full buffer.
- `OverwriteAt` allows in-place patching without advancing the write cursor, but throws for out-of-range offsets.
- `UnmanagedStream` requires `unsafe` context and raw pointer buffers.
- Span-based stream APIs require `#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER`.

### NativeBag
- `NativeBag.count` is in **bytes**, not item count. (3 ints = 12 bytes.)
- `ReserveEnqueue<T>(out UnsafeArrayIndex)` returns `ref T` — reserve space, write to it later via `AccessReserved<T>(idx)`.
- Can enqueue/dequeue different struct types (must dequeue in order).

### NativeDynamicArray
- Methods are **generic per-call**: `Add<int>(1)`, `Get<int>(0)`, `Set<int>(1, 99)`. Use `NativeDynamicArrayCast<T>` for a typed wrapper that eliminates per-call generics.
- In DEBUG, `Get<T>` validates the index against `Count<T>()` while `Set<T>` validates against `Capacity<T>()` — writing reserved-but-not-yet-added slots via `Set` is legal, reading them via `Get` is not.
- `AddWithoutGrow<T>(item)` throws if no space — use when you want manual growth control.
- `ToManagedArray<T>()` copies to a managed array.
- In DEBUG, type consistency is validated via hash.

### MB / NB Buffer Sentinels
- `AsReader()` / `AsWriter()` return `IDisposable` guards.
- **Multiple readers OK**; **writer is exclusive** (throws if a reader or another writer is active).
- This is a **debug-only** single-thread guard — it throws rather than blocks. In release, it's zero-cost.
- `MB<T>.Clear()` modifies the **backing array in-place** (zeros it).
- `NB<T>.Create` requires you to **manually allocate and free** both data and state pointers.
- `ManagedStrategy.FastClear()` for **reference types** nulls the slots (prevents GC leaks); for **value types** it may be a no-op.
- `ManagedStrategy.AsBytesPointer()` / `SerialiseFrom()` and `NativeStrategy.AsBytesPointer()` / `SerialiseFrom()` currently throw `NotImplementedException`.
- `ShiftRight(start, end)` moves `[start..end-1]` → `[start+1..end]`. `ShiftLeft(start, end)` moves `[start+1..end]` → `[start..end-1]`.

### MemoryUtilities Alignment
- `Align4(n)` returns the **aligned** value (next multiple of 4).
- `Pad4(n)` returns the **padding needed** (always 0–3).
- `Align4(0) = 0` (zero is already "aligned").

### SharedNativeInt
- `Increment()`, `Decrement()`, and `Add(n)` return the **new** value. `CompareExchange(new, comparand)` returns the previous value.
- `Set(n)` uses `Volatile.Write` (non-atomic write visible to other threads).

### Allocator Parameter
Native structures accept an `Allocator` enum. On Unity, `Temp`, `TempJob`, and `Persistent` map to their Unity allocator counterparts, while `Managed` maps to Unity `Persistent` when passed to `MemoryUtilities.NativeAlloc`. Outside Unity, native allocation uses `Marshal.AllocHGlobal` regardless of the enum value, so no temporary lifetime is automatic.

### ManagedSlotMap
- **Versioned handles**: Reused slots get incremented version. Old handles remain invalid even after the slot is reused.
- `Add(T)` returns `ValueIndex`. `Remove(ValueIndex)` invalidates it. `Has(ValueIndex)` checks validity.
- Handles are not tied to a particular map, and indexer/removal validation is Debug-only. Do not construct or transfer handles between maps; call `Has` before uncertain access in Release.
- Call `Dispose()` explicitly when the map is no longer needed; the type does not implement `IDisposable`.

### No Setup/Teardown in Tests
Tests are self-contained: each constructs, uses, and disposes inline. No shared state. This is the recommended pattern for testing Svelto data structures.
