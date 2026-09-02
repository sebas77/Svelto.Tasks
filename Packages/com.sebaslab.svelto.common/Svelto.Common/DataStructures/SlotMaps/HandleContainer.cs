using System;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures
{
    public struct SlotMap<T, StrategyD, StrategyS>
            where StrategyD : IBufferStrategy<T>, new()
            where StrategyS : IBufferStrategy<SparseIndex>, new()
    {
        public SlotMap(uint initialSize): this()
        {
            _sparse = new StrategyS();
            _sparse.Alloc(initialSize, Allocator.Persistent, true);
            _dense = new StrategyD();
            _dense.Alloc(initialSize, Allocator.Persistent, false);
        }

        public int capacity => _dense.capacity;
        public int count => (int)_count;

        public ref T this[ValueIndex index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DBC.Common.Check.Require(Has(index) == true, $"SparseSet - invalid index");

                return ref _dense[index.sparseIndex];
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (_count == 0)
            {
                _dense.Clear();
                return;
            }

            _count = 0;
            _freeList = default;

            _dense.Clear();

            for (uint index = 0; index < _nextUnused; index++)
            {
                var sparseIndex = _sparse[index];
                if (sparseIndex.IsValid() == false)
                    continue;

                if (sparseIndex.version == byte.MaxValue)
                {
                    _sparse[index] = default;
                    continue;
                }

                var freeIndex = new SparseIndex(sparseIndex, _freeList);
                _sparse[index] = freeIndex;
                _freeList = freeIndex;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Has(ValueIndex index)
        {
            return index.version > 0
                 && index.sparseIndex < capacity
                 && index.version == _sparse[index.sparseIndex].version;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueIndex Add(T val)
        {
            if (_freeList.IsValid())
            {
                _dense[_freeList.denseIndex] = val;

                ValueIndex ret = new ValueIndex(_freeList.denseIndex, _freeList.version);

                _freeList = _freeList.Next();

                ++_count;

                return ret;
            }

            var index = _nextUnused;
            if (index >= capacity)
                Reserve((uint)Math.Ceiling((capacity + 1) * 1.5f));

            ++_nextUnused;
            ++_count;

            _dense[index] = val;
            _sparse[index] = new SparseIndex(index, 1); //base count is 1 so 0 can be used as invalid

            return new ValueIndex(index, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Remove(ValueIndex index)
        {
            DBC.Common.Check.Require(Has(index) == true, $"SparseSet - invalid index: index {index}");

            var sparseIndex = _sparse[index.sparseIndex];

            if (sparseIndex.version == byte.MaxValue)
            {
                _sparse[index.sparseIndex] = default;
            }
            else
            {
                //invalidate value index as the sparse index version will increment
                //store the current _freelist to create a list of free spots
                var freeIndex = new SparseIndex(sparseIndex, _freeList);
                _sparse[index.sparseIndex] = freeIndex;
                //set the new free list to the just cleared spot
                _freeList = freeIndex;
            }

            --_count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reserve(uint u)
        {
            DBC.Common.Check.Require(u < MAX_SIZE, "Max size reached");

            if (u > capacity)
            {
                _dense.Resize(u);
                _sparse.Resize(u);
            }
        }

        public void Dispose()
        {
            _sparse.Dispose();
            _dense.Dispose();
        }

        StrategyD _dense;  //Dense set of elements (stable index == handle.sparseIndex)
        StrategyS _sparse; //Per-slot metadata + freelist links
        uint _count;       //LIVE elements count
        uint _nextUnused;  //First slot that has never been issued
        SparseIndex _freeList;

        static int MAX_SIZE = (int)Math.Pow(2, 24);
    }
}
