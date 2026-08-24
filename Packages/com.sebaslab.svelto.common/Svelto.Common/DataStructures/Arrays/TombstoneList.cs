#if DEBUG && !PROFILE_SVELTO
#define ENABLE_DEBUG_CHECKS
#endif

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public struct TombstoneItem<T>
    {
        public T Item;
        public int NextUnusedIndex; // 0 indicates the cell is used, otherwise points to the next unused cell (+1)
    }

    public readonly struct TombstoneHandle : IEquatable<TombstoneHandle>, IComparable<TombstoneHandle>
    {
        public static readonly TombstoneHandle Invalid = new TombstoneHandle(-1);

        public TombstoneHandle(int index)
        {
            this.index = index;
        }

        public static explicit operator int(TombstoneHandle handle) => (int)handle.index;
        public static explicit operator uint(TombstoneHandle handle) => (uint)handle.index;

        public readonly int index;
        public bool IsInvalid => index == Invalid.index;

        public static bool operator ==(TombstoneHandle left, TombstoneHandle right) => left.index == right.index;
        public static bool operator !=(TombstoneHandle left, TombstoneHandle right) => left.index != right.index;

        public bool Equals(TombstoneHandle other)
        {
            return index == other.index;
        }

        public override bool Equals(object obj)
        {
            return obj is TombstoneHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return index;
        }

        public int CompareTo(TombstoneHandle other)
        {
            return index.CompareTo(other.index);
        }
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
            Array.Clear(_buffer, 0, _buffer.Length); //must reset the NextUnusedIndex flags too
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AllocateMore(int newLength)
        {
            TombstoneItem<T>[] newList = new TombstoneItem<T>[newLength];
            int oldLength = _buffer.Length;
            Array.Copy(_buffer, newList, oldLength);

            _buffer = newList;
        }

        [Conditional("ENABLE_DEBUG_CHECKS")]
        void ValidateIndexAndTombstone(TombstoneHandle index)
        {
            DBC.Common.Check.Require(index.IsInvalid == false, "index must be not negative");

            // this must always be validated to guarantee safety even without ENABLE_DEBUG_CHECKS
            DBC.Common.Check.Require(index.index < _buffer.Length,
                $"out of bound index {index} - capacity {_buffer.Length}");

#if ENABLE_DEBUG_CHECKS
            DBC.Common.Check.Require(index.index < _largestUsedIndex,
                $"out of bound index {index} - largest used index {_largestUsedIndex}");
#endif

            DBC.Common.Check.Require(_buffer[index.index].NextUnusedIndex == 0,
                $"trying to access a tombstone at index {index} that is already removed");
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

            // this slot is not part of the free list anymore, setting to 0 can be used to check if the slot is used or not 
            // we use 0 as "used" flag inside the TombstoneListEnumerator
            ref int nextUnusedIndex = ref _buffer[indexToUse].NextUnusedIndex;
         
            if (nextUnusedIndex > 0) //the linked list is pointing to another unused slot
            {
                _firstUnusedIndex = nextUnusedIndex; //take note of the next unused slot
                nextUnusedIndex = 0;
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
#if ENABLE_DEBUG_CHECKS
            if (indexToUse >= _largestUsedIndex)
                _largestUsedIndex = indexToUse + 1; //_largestUsedIndex is in base 1
#endif

            return new TombstoneHandle(indexToUse);
        }
        

        internal TombstoneItem<T>[] _buffer;
        int _firstUnusedIndex; //in base 1, 0 means no free slots
        int _count; // number of used slots
      

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
        public TombstoneHandle currentHandle => new TombstoneHandle(_index);

        public bool MoveNext()
        {
#if ENABLE_DEBUG_CHECKS
            if (_owner._version != _capturedVersion)
                throw new InvalidOperationException("Collection was modified during enumeration");
#endif
            // advance to next used slot
            while (++_index < _owner._buffer.Length)
            {
                if (_owner._buffer[_index].NextUnusedIndex == 0) // live element
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

