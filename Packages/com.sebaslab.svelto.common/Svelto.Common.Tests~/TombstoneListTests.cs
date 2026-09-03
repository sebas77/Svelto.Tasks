using System;
using System.Collections.Generic;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    /// <summary>
    /// A collection of unit tests for the <see cref="TombstoneList{T}"/> class.
    /// These tests exercise the primary behaviours described in the data structure:
    /// <list type="bullet">
    ///   <item><description>Adding new items when no free slots exist grows the underlying buffer and returns distinct handles for each item.</description></item>
    ///   <item><description>Removing an item leaves a tombstone that can be reused on subsequent adds.</description></item>
    ///   <item><description>Enumeration skips over tombstoned slots and is invalidated when the collection is modified.</description></item>
    ///   <item><description>Clearing the list resets the count and frees all slots.</description></item>
    ///   <item><description>Using an invalid or removed handle through the indexer throws an exception.</description></item>
    /// </list>
    /// A tombstone list is a data‑structure that supports O(1) removals by leaving behind
    /// a special marker (a tombstone) instead of physically removing the slot. This marker
    /// indicates that the slot once contained a value but is now free for reuse.  The concept
    /// originates from hash tables where tombstones allow search operations to proceed past
    /// removed entries and where insertions are allowed to reuse deleted slots.
    /// </summary>
    [TestFixture]
    public class TombstoneListTests
    {
        /// <summary>
        /// Verifies that adding a single element to a new list correctly stores the value,
        /// increments the count and returns a valid, non‑invalid handle.
        /// </summary>
        [Test]
        public void Add_SingleItem_ShouldStoreItemAndIncrementCount()
        {
            var list = new TombstoneList<int>();

            TombstoneHandle handle = list.Add(42);

            Assert.That(list.count, Is.EqualTo(1), "Count should be 1 after adding a single item.");
            Assert.That(handle.IsInvalid, Is.False, "Add should return a valid handle.");
            Assert.That(list[handle], Is.EqualTo(42), "Item at handle should match the inserted value.");
        }

        /// <summary>
        /// When adding two items without removing any, the list should assign distinct
        /// handles and the count should reflect two live entries.  This test also
        /// confirms that items can be retrieved via their respective handles.
        /// </summary>
        [Test]
        public void Add_TwoItems_ShouldHaveCount2AndDistinctHandles()
        {
            var list = new TombstoneList<int>();

            TombstoneHandle handle1 = list.Add(10);
            TombstoneHandle handle2 = list.Add(20);

            // According to the tombstone design, each new insertion without reuse
            // should occupy a fresh slot.  We expect two distinct handles and two live items.
            Assert.That(list.count, Is.EqualTo(2), "Count should be 2 after inserting two items.");
            Assert.That(handle1, Is.Not.EqualTo(handle2), "Handles for different items must be distinct.");
            Assert.That(list[handle1], Is.EqualTo(10), "First item's value is incorrect.");
            Assert.That(list[handle2], Is.EqualTo(20), "Second item's value is incorrect.");
        }

        /// <summary>
        /// Adding via <see cref="TombstoneList{T}.AddByRef"/> should allow callers to modify the
        /// element in place through the returned reference and retrieve it later by handle.
        /// </summary>
        [Test]
        public void AddByRef_ShouldReturnRef_AndUpdateItem()
        {
            var list = new TombstoneList<int>();

            // Add by ref and update the value through the returned reference.
            ref int valueRef = ref list.AddByRef(out TombstoneHandle handle);
            valueRef = 99;

            Assert.That(list.count, Is.EqualTo(1), "Count should be 1 after AddByRef.");
            Assert.That(list[handle], Is.EqualTo(99), "Value set via reference should be retrievable.");
        }

        /// <summary>
        /// After removing an entry, the slot should be marked as a tombstone and reused
        /// on subsequent insertions.  This property is fundamental to tombstone lists:
        /// the freed slot should be available to a future insertion.
        /// </summary>
        [Test]
        public void RemoveAt_ShouldFreeSlotAndReuseOnNextAdd()
        {
            var list = new TombstoneList<int>();
            var handle1 = list.Add(1);
            var handle2 = list.Add(2);

            // Remove the first item, leaving a tombstone in its place.
            list.RemoveAt(handle1);
            Assert.That(list.count, Is.EqualTo(1), "Count should decrease to 1 after removal.");

            // Add another element; it should reuse the slot freed by handle1.
            var handle3 = list.Add(3);

            Assert.That(list.count, Is.EqualTo(2), "Count should be 2 after re‑adding an item.");
            Assert.That((int)handle3, Is.EqualTo((int)handle1), "Removed slot should be reused for the next insertion.");
            Assert.That(handle3, Is.Not.EqualTo(handle1), "Reused slots must receive a new generation.");
            Assert.That(list[handle3], Is.EqualTo(3), "New value should be stored in the reused slot.");
            Assert.That(list[handle2], Is.EqualTo(2), "Second handle should still refer to its original value.");
        }

        [Test]
        public void RemoveAt_Twice_Throws()
        {
            var list = new TombstoneList<int>(2);

            TombstoneHandle index = list.Add(123);
            list.RemoveAt(index);

            Assert.That(() => list.RemoveAt(index), Throws.TypeOf<DBC.Common.PreconditionException>());
        }

        [Test]
        public void StaleHandle_CannotAccessOrRemoveAReusedSlot()
        {
            var list = new TombstoneList<int>();
            var stale = list.Add(1);

            list.RemoveAt(stale);
            var replacement = list.Add(2);

            Assert.That((int)replacement, Is.EqualTo((int)stale), "The free slot should be reused.");
            Assert.That(list.Has(stale), Is.False, "The removed handle must remain invalid after slot reuse.");
            Assert.That(list.Has(replacement), Is.True, "The newly issued handle must be valid.");
            Assert.Throws<DBC.Common.PreconditionException>(() => { var _ = list[stale]; });
            Assert.Throws<DBC.Common.PreconditionException>(() => list.RemoveAt(stale));
            Assert.That(list[replacement], Is.EqualTo(2), "A stale removal must not affect the replacement item.");
        }

        [Test]
        public void StaleHandle_RemainsInvalidAfterClearAndReuse()
        {
            var list = new TombstoneList<int>();
            var stale = list.Add(1);

            list.Clear();
            var replacement = list.Add(2);

            Assert.That((int)replacement, Is.EqualTo((int)stale), "Clear restarts allocation from the first slot.");
            Assert.That(list.Has(stale), Is.False, "Clear must invalidate handles from the previous contents.");
            Assert.That(list.Has(replacement), Is.True);
            Assert.Throws<DBC.Common.PreconditionException>(() => { var _ = list[stale]; });
            Assert.That(list[replacement], Is.EqualTo(2));
        }

        [Test]
        public void ManuallyConstructedHandle_CannotForgeTheFirstLiveSlot()
        {
            var list = new TombstoneList<int>();
            var generated = list.Add(42);
            var manuallyConstructed = new TombstoneHandle((int)generated);

            Assert.That(manuallyConstructed.IsInvalid, Is.True, "A bare index has no generation and must be invalid.");
            Assert.That(list.Has(manuallyConstructed), Is.False);
            Assert.Throws<DBC.Common.PreconditionException>(() => { var _ = list[manuallyConstructed]; });
            Assert.That(list[generated], Is.EqualTo(42), "Rejecting a forged handle must not affect the generated handle.");
        }

        [Test]
        public void EnumeratorHandle_IdentifiesTheCurrentLiveGeneration()
        {
            var list = new TombstoneList<int>();
            list.Add(10);
            list.Add(20);

            var enumerator = list.GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True);
            var current = enumerator.currentHandle;

            Assert.That(list.Has(current), Is.True);
            Assert.That(list[current], Is.EqualTo(10));
        }

        [Test]
        public void EveryPriorGeneration_RemainsStaleAcrossRepeatedReuse()
        {
            var list = new TombstoneList<int>();
            var staleHandles = new List<TombstoneHandle>();
            var current = list.Add(0);

            for (int value = 1; value <= 32; value++)
            {
                list.RemoveAt(current);
                staleHandles.Add(current);
                current = list.Add(value);

                Assert.That(list.Has(current), Is.True);
                Assert.That(list[current], Is.EqualTo(value));
            }

            foreach (var stale in staleHandles)
            {
                Assert.That(list.Has(stale), Is.False);
                Assert.Throws<DBC.Common.PreconditionException>(() => { var _ = list[stale]; });
            }
        }

        /// <summary>
        /// Enumerating the list should yield only live items (non‑tombstoned slots) in the order
        /// of their indices.  This test also ensures that the enumerator’s Reset method
        /// correctly restarts iteration from the beginning.
        /// </summary>
        [Test]
        public void GetEnumerator_ShouldSkipTombstones_AndSupportReset()
        {
            var list = new TombstoneList<int>();
            list.Add(100);
            var h2 = list.Add(200);
            list.Add(300);

            // Remove the second element so that a tombstone exists at its slot.
            list.RemoveAt(h2);

            var enumerator = list.GetEnumerator();
            var values = new List<int>();
            while (enumerator.MoveNext())
            {
                values.Add(enumerator.Current);
            }

            // Only two live items should be enumerated, skipping the tombstone.
            Assert.That(values, Is.EquivalentTo(new[] { 100, 300 }), "Enumerator should skip removed items.");

            // Reset the enumerator and iterate again.
            enumerator.Reset();
            values.Clear();
            while (enumerator.MoveNext())
            {
                values.Add(enumerator.Current);
            }
            Assert.That(values, Is.EquivalentTo(new[] { 100, 300 }), "Enumerator Reset should restart iteration.");
        }

        /// <summary>
        /// According to .NET enumerator semantics, modifying a collection during enumeration
        /// should cause the enumerator to be invalidated and throw an <see cref="InvalidOperationException"/>.
        /// </summary>
#if DEBUG
        [Test]
        public void Enumerator_ShouldThrowWhenCollectionModified()
        {
            var list = new TombstoneList<int>();
            list.Add(1);
            list.Add(2);

            var enumerator = list.GetEnumerator();
            Assert.That(enumerator.MoveNext(), Is.True, "Enumerator should initially move to first element.");

            // Mutate the collection – this should invalidate the enumerator.
            list.Add(3);

            bool thrown = false;
            try
            {
                enumerator.MoveNext();
            }
            catch (InvalidOperationException)
            {
                thrown = true;
            }
            
            Assert.That(thrown, Is.True, "Modifying the collection during enumeration should invalidate the enumerator and throw.");
        }
#endif

        /// <summary>
        /// Clearing the list should reset the count to zero and allow subsequent additions to start
        /// from index zero again.  Re‑adding items after a clear should behave like adding to a new list.
        /// </summary>
        [Test]
        public void Clear_ShouldResetList()
        {
            var list = new TombstoneList<string>();
            list.Add("first");
            list.Add("second");

            Assert.That(list.count, Is.EqualTo(2), "Count should be 2 before clearing.");

            list.Clear();

            Assert.That(list.count, Is.EqualTo(0), "Count should be 0 after clearing.");

            // After clearing, adding should start over from the first slot.
            var h3 = list.Add("third");
            Assert.That(h3.IsInvalid, Is.False, "Handle returned after Clear should be valid.");
            Assert.That(list[h3], Is.EqualTo("third"), "Value inserted after Clear should be retrievable.");
            Assert.That(list.count, Is.EqualTo(1), "Count should be 1 after adding to cleared list.");
        }

        /// <summary>
        /// Using an invalid handle or a handle referring to a removed slot through the indexer
        /// should result in an exception due to the tombstone list's guard checks.  A tombstone
        /// handle is considered invalid if it equals <see cref="TombstoneHandle.Invalid"/> or points
        /// to a slot that has been removed.
        /// </summary>
        [Test]
        public void Indexer_ShouldThrow_WhenHandleIsInvalidOrRemoved()
        {
            var list = new TombstoneList<int>();
            var handle = list.Add(5);

            // Removed handle should cause an exception when accessed.
            list.RemoveAt(handle);
            Assert.Throws<DBC.Common.PreconditionException>(
                () => { var _ = list[handle]; },
                "Accessing a removed handle should throw.");

            // Explicitly invalid handle should cause an exception.
            Assert.Throws<DBC.Common.PreconditionException>(
                () => { var _ = list[TombstoneHandle.Invalid]; },
                "Accessing an invalid handle should throw.");
        }

        /// <summary>
        /// Adding a large number of items beyond the initial capacity should increase
        /// the underlying buffer size.  We verify that capacity grows when necessary.
        /// </summary>
        [Test]
        public void Add_ManyItems_ShouldGrowCapacity()
        {
            var initialSize = 2;
            var list = new TombstoneList<int>(initialSize);

            // Add more elements than the initial capacity to trigger a resize.
            for (int i = 0; i < 10; i++)
            {
                list.Add(i);
            }

            Assert.That(list.capacity, Is.GreaterThanOrEqualTo(10), "Capacity should grow to accommodate the number of added items.");
            Assert.That(list.count, Is.EqualTo(10), "Count should reflect the number of added items.");

            // Validate that each inserted item can be retrieved correctly by enumerating all live entries.
            var actualValues = new List<int>();
            foreach (var item in list)
            {
                actualValues.Add(item);
            }
            // Since the order may depend on reused slots, ensure all expected values are present.
            Assert.That(actualValues, Is.EquivalentTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
        }
    }
}
