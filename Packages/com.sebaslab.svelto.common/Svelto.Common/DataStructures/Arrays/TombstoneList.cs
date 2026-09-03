#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public struct TombstoneItem<T>
    {
        public T Item;
        // A non-negative value is the next free slot in the base-one free list. A negative value marks a live
        // slot and carries its generation token. Reusing this field preserves the existing item layout.
        public int NextUnusedIndex;
    }

    public readonly struct TombstoneHandle : IEquatable<TombstoneHandle>, IComparable<TombstoneHandle>
    {
        public static readonly TombstoneHandle Invalid = new TombstoneHandle(-1, 0);

        // A bare index cannot identify a slot after it has been recycled. Preserve source compatibility, but
        // reserve generation zero for manually constructed/default handles so TombstoneList never accepts them.
        public TombstoneHandle(int index) : this(index, 0)
        {
        }

        internal TombstoneHandle(int index, int generation)
        {
            this.index = index;
            _generation = generation;
        }

        public static explicit operator int(TombstoneHandle handle) => (int)handle.index;
        public static explicit operator uint(TombstoneHandle handle) => (uint)handle.index;

        public readonly int index;
        // Full validity depends on the owning TombstoneList slot; call TombstoneList.Has for that check.
        public bool IsInvalid => index < 0 || _generation == 0;

        public static bool operator ==(TombstoneHandle left, TombstoneHandle right) => left.index == right.index && left._generation == right._generation;
        public static bool operator !=(TombstoneHandle left, TombstoneHandle right) => (left == right) == false;

        public bool Equals(TombstoneHandle other)
        {
            return this == other;
        }

        public override bool Equals(object obj)
        {
            return obj is TombstoneHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (index * 397) ^ _generation;
            }
        }

        public int CompareTo(TombstoneHandle other)
        {
            var compare = index.CompareTo(other.index);
            return compare != 0 ? compare : _generation.CompareTo(other._generation);
        }

        internal int generation => _generation;

        readonly int _generation;
    }

    /// <summary>
    /// Stores items in stable slots, allowing O(1) removal without moving the remaining items.
    /// Removed slots become tombstones and are reused by later additions, so existing <see cref="TombstoneHandle"/>
    /// values stay valid until their corresponding items are removed. Use this when callers need to retain handles
    /// across additions and removals; use a compact list instead when contiguous ordering is required.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TombstoneList<T>
    {
        public TombstoneList()
        {
            _count = 0;
            _firstUnusedIndex = 1;

            _buffer = Array.Empty<TombstoneItem<T>>();
        }

        public TombstoneList(uint initialSize) : this((int)initialSize)
        { }

        public TombstoneList(int initialSize)
        {
            _count = 0;
            _firstUnusedIndex = 1;

            _buffer = new TombstoneItem<T>[initialSize];
        }

        //count in this class is extremely tricky because it represents the number of used slots
        //not the number of total slots allocated
        public int count => (int)_count;
        public int capacity => _buffer.Length;
        
        public ref T this[TombstoneHandle index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ValidateIndexAndTombstone(index);

                return ref _buffer[index.index].Item;
            }
        }

        /// <summary>
        /// Returns true only while this list owns the exact slot generation represented by the handle.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(TombstoneHandle handle)
        {
            if (handle.IsInvalid || (uint)handle.index >= (uint)_buffer.Length)
                return false;

            var slotMetadata = _buffer[handle.index].NextUnusedIndex;
            return IsLive(slotMetadata) && GetGeneration(slotMetadata) == handle.generation;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TombstoneHandle Add(in T item)
        {
            TombstoneHandle index = TakeFreeSlot();
            _buffer[(int)index].Item = item;
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            return index;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AddByRef(out TombstoneHandle handle)
        {
            TombstoneHandle index = TakeFreeSlot();
#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
            handle = index;
            return ref _buffer[(int)index].Item;
        }
        
        //To better visualize how _firstUnusedIndex and NextUnusedIndex work, is best to start from RemoveAt
        //once a slot is removed, its NextUnusedIndex points to the previous first unused slot and 
        //then _firstUnusedIndex is updated to point to this newly freed slot (index + 1 because it's in base 1)
        //effectively creating a linked list of unused slots
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(TombstoneHandle handle)
        {
            ValidateIndexAndTombstone(handle);
            
            var index = (int)handle;
            ref var flaggedItem = ref _buffer[index];

            flaggedItem.Item = default; //clear the item as it could hold references to other objects
            //Link this newly freed slot to the previous first unused slot

            //Make this slot the new first unused slot (add 1 because indices are stored in base 1)
            var old = _firstUnusedIndex;
            _firstUnusedIndex = index + 1;
            int nextUnusedIndex = old; //updating linked list and first empty slot in base 1
            flaggedItem.NextUnusedIndex = nextUnusedIndex;

            //Decrease the total count of used slots
            _count--;

#if ENABLE_DEBUG_CHECKS
            _version++;
#endif
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TombstoneListEnumerator<T> GetEnumerator()
        {
            return new TombstoneListEnumerator<T>(this);
        }
        
        public void Clear()
        {
            _count = 0;
            _firstUnusedIndex = 1;
#if ENABLE_DEBUG_CHECKS
            _largestUsedIndex = 0;
            _version++;
#endif
            // Do not reset _nextGeneration: a handle issued before Clear must not match a slot issued afterwards.
            Array.Clear(_buffer, 0, _buffer.Length);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateMore(int newLength)
        {
            TombstoneItem<T>[] newList = new TombstoneItem<T>[newLength];
            int oldLength = _buffer.Length;
            Array.Copy(_buffer, newList, oldLength);

            _buffer = newList;
        }

        void ValidateIndexAndTombstone(TombstoneHandle index)
        {
            // This remains active in Release: a debug-only guard would let a stale handle access a replacement item.
            if (Has(index) == false)
                throw new DBC.Common.PreconditionException($"invalid, removed, or stale tombstone handle at index {index.index}");
        }
        
        //then if we want to add a new item, we check if there are any unused slots from the linked list
        //if there are, we take the first one pointed by _firstUnusedIndex (base 1, so we convert to base-0)
        //which represent the last removed slot. (the top of the linked list points to the last removed slot)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        TombstoneHandle TakeFreeSlot()
        {
            // count = 0 first unused = 1 (base 1, starting condition)
            // add first element => count = 1 first unused = 2 (base 1)
            _count++;
            
            //if _firstUnusedIndex (base 1) is beyond the buffer length, we need to grow the buffer
            if (_firstUnusedIndex > _buffer.Length)
                AllocateMore((int)((_buffer.Length + 1) * 1.5f));

            int indexToUse = _firstUnusedIndex - 1; // convert base 1 → base-0

            // This slot is no longer free. Give it a new token so any older handle for the same slot stays stale.
            ref int nextUnusedIndex = ref _buffer[indexToUse].NextUnusedIndex;
          
            if (nextUnusedIndex > 0) //the linked list is pointing to another unused slot
            {
                _firstUnusedIndex = nextUnusedIndex; //take note of the next unused slot
            }
            else
            {
#if ENABLE_DEBUG_CHECKS
                //if there are no slots to reuse, count (before the increment) must match largest used index in base 1 (count - 1 = 5, largest used = 4 + 1)
                DBC.Common.Check.Require(_largestUsedIndex == _count - 1, "inconsistent state in TombstoneList");
#endif
                //no slots to reuse available, we must use the first never used slot
                //Attention: for the case where the list is packed (no tombstone) the first unused index must be the current index used 
                // + 1 (unused) + 1 (base 1). However since _firstUnusedIndex == count + 1 and count has already been incremented at the start of this method,
                //_firstUnusedIndex will be just count + 1
                _firstUnusedIndex = _count + 1; //_firstUnusedIndex is in base 1
            }

            nextUnusedIndex = EncodeLiveGeneration(NextGeneration());
#if ENABLE_DEBUG_CHECKS
            if (indexToUse >= _largestUsedIndex)
                _largestUsedIndex = indexToUse + 1; //_largestUsedIndex is in base 1
#endif

            return new TombstoneHandle(indexToUse, GetGeneration(nextUnusedIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsLive(int slotMetadata)
        {
            return slotMetadata < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetGeneration(int slotMetadata)
        {
            return slotMetadata & int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int EncodeLiveGeneration(int generation)
        {
            return int.MinValue | generation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int NextGeneration()
        {
            // Generation zero is reserved for default/manually constructed handles, so generated handles always
            // carry an identity token. Only after over two billion allocations can the counter wrap.
            if (_nextGeneration == int.MaxValue)
                _nextGeneration = 1;
            else
                _nextGeneration++;

            return _nextGeneration;
        }
        

        internal TombstoneItem<T>[] _buffer;
        int _firstUnusedIndex; //in base 1, 0 means no free slots
        int _count; // number of used slots
        int _nextGeneration;
      

#if ENABLE_DEBUG_CHECKS
        int _largestUsedIndex; // largest index ever base 1
        internal int _version;
#endif
    }
    
    public ref struct TombstoneListEnumerator<T>
    {
        internal TombstoneListEnumerator(TombstoneList<T> owner)
        {
            _owner = owner;
#if ENABLE_DEBUG_CHECKS
            _capturedVersion = owner._version;
#endif
            _index = -1;
            _returned = 0;
        }

        public ref T Current => ref _owner._buffer[_index].Item; //current as capital C for foreach support
        public TombstoneHandle currentHandle => new TombstoneHandle(_index,
            _owner._buffer[_index].NextUnusedIndex & int.MaxValue);

        public bool MoveNext()
        {
#if ENABLE_DEBUG_CHECKS
            if (_owner._version != _capturedVersion)
                throw new InvalidOperationException("Collection was modified during enumeration");
#endif
            // advance to next used slot
            while (++_index < _owner._buffer.Length)
            {
                if (_owner._buffer[_index].NextUnusedIndex < 0) // live element
                {
                    if (++_returned > _owner.count) // safety net
                        return false;

                    return true;
                }
            }

            return false; // end of buffer
        }

        public void Reset()
        {
            _index = -1;
            _returned = 0;
        }

        readonly TombstoneList<T> _owner; // gives us live access to version & data
#if ENABLE_DEBUG_CHECKS
        readonly int _capturedVersion;
#endif
        int _index;
        uint _returned;
    }
}

