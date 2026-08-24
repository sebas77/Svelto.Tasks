# Svelto.Common.Tests

NUnit test project for `Svelto.Common`.

Currently covers:
- `TombstoneList<T>`
- `FasterList<T>` (+ read-only variants and extensions)
- `MonotonicWindowBuffer<T>`
- Ring buffers (`RingBuffer<T>`, `CircularQueue<T>`, `UnmanagedCircularQueue<TCell>`, `UnmanagedConcurrentCircularQueue<TCell>`)
- Streams (`SveltoStream`, `ManagedStream`, `UnmanagedStream`)
- DualMemorySupport managed side (`ManagedStrategy<T>`, `MB<T>`)
