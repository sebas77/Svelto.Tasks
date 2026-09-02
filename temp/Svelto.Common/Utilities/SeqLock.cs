using System.Threading;
using Svelto.Utilities;

// Seqlock (sequence lock) for "many readers, rare writers" with optimistic reads.
//
// What it protects: a group of related shared values, not one shared variable. A writer marks
// the sequence as odd, updates every field, then marks it even. A reader copies the fields
// between two sequence reads and retries when the value changed.
//
// Practical example: a simulation thread publishes an entity's position and velocity. Rendering,
// audio and networking threads need a consistent snapshot, so they copy the complete state and
// only use it after ValidateRead confirms that no simulation update overlapped their copy.
//
// ==============================
//  WHY THIS EXISTS (DRAWINGS)
// ==============================
//
//  A) Why Interlocked-based reader counters contend (cache line ping-pong)
//  --------------------------------------------------------------
//  Imagine a single shared counter living in one 64B cache line.
//
//      Cache line L (64 bytes)
//     +----------------------------------------------------+
//     | ... counter ...                                    |
//     +----------------------------------------------------+
//
//  If every reader does Interlocked.Increment/Decrement, each reader needs
//  *exclusive ownership* of that cache line to perform the RMW (read-modify-write).
//
//  Time →
/*
    C0: [ owns L exclusively ] INC ──┐             DEC ──┐
    C1:            ┌─────────────────┘ [ owns L ] INC ──┐ └─── ...
    C2:                             ┌───────────────────┘ [ owns L ] ...
    C3:                                                  ...
*/
//
//  The line "bounces" between cores. More cores => more bouncing => worse scaling.
//  You're not blocked on an OS mutex; you're stalled on cache-coherency traffic.
//
//  B) Why a seqlock removes reader-side contention
//  ----------------------------------------------
//  Readers do *no RMW*. They only read a sequence number twice.
//
//      Cache line S (contains `seq`)
//     +----------------------------------------------------+
//     | seq                                                |
//     +----------------------------------------------------+
//
//  Readers (loads only):
//
//      READ seq -> read data -> READ seq -> compare
//
//  Loads can be shared across cores, so there is no cache-line ownership ping-pong.
//  Only writers occasionally take exclusive ownership of `seq`.
//
//  C) False sharing warning
//  -----------------------
//  If `seq` shares a cache line with some other hot RMW counter, that other counter
//  will bounce the whole line and readers will suffer even though they only read `seq`.
//
// ==============================
//  WHEN TO USE THIS
// ==============================
//  Use when ALL are true:
//   - Reads dominate writes (writes are rare).
//   - Readers can tolerate retrying occasionally.
//   - A "torn" read is not catastrophic as long as it is detected and retried.
//     (i.e. you won't go out-of-bounds / crash if you momentarily see inconsistent fields;
//      you only *use* the snapshot after validation passes).
//
//  Do NOT use when:
//   - Writes are frequent (readers may retry a lot).
//   - You need fairness (writers/readers can starve depending on workload).
//   - Readers must never observe intermediate states (even transiently).
//
//  IMPORTANT USAGE RULE:
//   - Readers must copy all required fields into locals, then validate.
//   - Only use the copied snapshot after ValidateRead() succeeds.
//
//  Threading model:
//   - Multiple readers.
//   - Writers serialized (one writer active at a time).
//
// Backoff policy:
//   - Uses your ThreadUtility.Wait (Yield/Spin/Relax depending on your platform policy).
//
sealed class SeqLock
{
    // Even => stable (no writer active)
    // Odd  => writer active
    int _seq;

    // ----------------
    // Reader-side API
    // ----------------

    // Try to start a read. Returns false if a writer is active (odd seq).
    public bool TryBeginRead(out int seq0)
    {
        seq0 = Volatile.Read(ref _seq);
        if ((seq0 & 1) != 0)
            return false;

        return true;
    }

    // After you've copied the data you need into locals, validate.
    // True => the snapshot is consistent and can be used.
    public bool ValidateRead(int seq0)
    {
        int seq1 = Volatile.Read(ref _seq);
        return seq0 == seq1;
    }

    // Wait until a stable (even) sequence is observed, then return it.
    // powerOf2Frequency must be power of 2 (same constraint as ThreadUtility.Wait).
    public int BeginReadWait(ref int quickIterations, int powerOf2Frequency = 256)
    {
        while (true)
        {
            int seq0 = Volatile.Read(ref _seq);
            if ((seq0 & 1) == 0)
                return seq0;

            ThreadUtility.Wait(ref quickIterations, powerOf2Frequency);
        }
    }

    // ----------------
    // Writer-side API
    // ----------------

    // Enter write: flips seq from even->odd using CAS (serializes writers).
    // powerOf2Frequency must be power of 2 (same constraint as ThreadUtility.Wait).
    public void EnterWrite(ref int quickIterations, int powerOf2Frequency = 256)
    {
        while (true)
        {
            int current = Volatile.Read(ref _seq);

            if ((current & 1) != 0)
            {
                ThreadUtility.Wait(ref quickIterations, powerOf2Frequency);
                continue;
            }

            if (Interlocked.CompareExchange(ref _seq, current + 1, current) == current)
                return;

            ThreadUtility.Wait(ref quickIterations, powerOf2Frequency);
        }
    }

    // Exit write: increment odd->even.
    public void ExitWrite()
    {
        Interlocked.Increment(ref _seq);
    }
}

/*
USAGE EXAMPLE (pattern you should follow):

// Shared mutable state published by a simulation thread:
struct EntitySnapshot
{
    public float positionX;
    public float positionY;
    public float positionZ;
    public float velocityX;
    public float velocityY;
    public float velocityZ;
}

SeqLock _snapshotLock = new SeqLock();
EntitySnapshot _latestSnapshot;

// Simulation thread: publish position and velocity as one coherent snapshot.
void PublishSnapshot(EntitySnapshot snapshot)
{
    int it = 0;
    _snapshotLock.EnterWrite(ref it);
    try
    {
        _latestSnapshot = snapshot;
    }
    finally
    {
        _snapshotLock.ExitWrite();
    }
}

// Rendering, audio or networking thread: read a coherent snapshot without blocking other readers.
bool TryReadSnapshot(out EntitySnapshot snapshot)
{
    int it = 0;
    while (true)
    {
        int seq0;
        if (_snapshotLock.TryBeginRead(out seq0) == false)
            seq0 = _snapshotLock.BeginReadWait(ref it);

        // Copy every related field before validating, never one field at a time.
        snapshot = _latestSnapshot;

        if (_snapshotLock.ValidateRead(seq0))
            return true;

        // The simulation update overlapped the copy: retry instead of mixing old and new fields.
        ThreadUtility.Wait(ref it);
    }
}

Notes:
- Don't access `_latestSnapshot` after ValidateRead fails.
- Keep the read region small (copy only what you need).
- If `_latestSnapshot` contains references to mutable objects, this only validates the reference values,
  not the internal mutation of those objects. Prefer immutable snapshots or value-type blobs.
*/
