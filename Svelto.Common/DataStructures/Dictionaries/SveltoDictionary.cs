using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Svelto.Common;
using Svelto.Utilities;

namespace Svelto.DataStructures
{
    sealed class SveltoDictionaryDebugProxy<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy>
            where TKey : struct, IEquatable<TKey>
            where TKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
            where TValueStrategy : struct, IBufferStrategy<TValue>
            where TBucketStrategy : struct, IBufferStrategy<int>
    {
        public SveltoDictionaryDebugProxy(SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> dic)
        {
            _dic = dic;
        }

        public uint count => (uint)_dic.count;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePairFast<TKey, TValue, TValueStrategy>[] keyValues
        {
            get
            {
                var array = new KeyValuePairFast<TKey, TValue, TValueStrategy>[_dic.count];

                int i = 0;
                foreach (var keyValue in _dic)
                {
                    array[i++] = keyValue;
                }

                return array;
            }
        }

        readonly SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> _dic;
    }

    /// <summary>
    /// This dictionary has been created for just one reason: I needed a dictionary that would have let me iterate
    /// over the values as an array, directly, without generating one or using an iterator.
    /// For this goal is N times faster than the standard dictionary. Faster dictionary might also be faster than
    /// the standard dictionary for most of the operations, but the difference is negligible. The only slower operation
    /// is resizing the memory on add, as this implementation needs to use two separate arrays compared to the standard
    /// one, this might be solved in future if I start to use segments.
    /// note: SveltoDictionary is not thread safe. A thread safe version should take care of possible setting of
    /// value with shared hash hence bucket list index.
    /// </summary>
    [DebuggerTypeProxy(typeof(SveltoDictionaryDebugProxy<,,,,>))]
    public struct SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy>: IDisposable,
            ISveltoDictionary<TKey, TValue>
            where TKey : struct, IEquatable<TKey>
            where TKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
            where TValueStrategy : struct, IBufferStrategy<TValue>
            where TBucketStrategy : struct, IBufferStrategy<int>
    {
        static SveltoDictionary()
        {
            NoBurstCheck();
        }
#if UNITY_BURST
        [Unity.Burst.BurstDiscard]
#endif
        static void NoBurstCheck()
        {
#if DEBUG && !PROFILE_SVELTO
            try
            {
                if (typeof(TKey).GetMethod("GetHashCode"
                      , System.Reflection.BindingFlags.Public |  System.Reflection.BindingFlags.Instance |  System.Reflection.BindingFlags.DeclaredOnly)
                 == null)
                    Svelto.Console.LogWarning(typeof(TKey).Name
                      + " does not implement GetHashCode -> This will cause unwanted allocations (boxing)");
            }
            catch ( System.Reflection.AmbiguousMatchException) { }
#endif
        }

        public SveltoDictionary(uint size, Allocator allocator): this()
        {
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision = SharedNativeInt.Create(0, allocator);
#endif
            //AllocationStrategy must be passed external for TValue because SveltoDictionary doesn't have struct
            //constraint needed for the NativeVersion
            _valuesInfo = default;
            _valuesInfo.Alloc(size, allocator);
            _values = default;
            _values.Alloc(size, allocator);
            _buckets = default;
            _buckets.Alloc((uint)HashHelpers.GetPrime((int)size), allocator);

            if (size > 0)
                _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier(size);
        }

        public TKeyStrategy unsafeKeys
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _valuesInfo;
        }

        //in SveltoDictionary, values are always safe to iterate, use dictionary count for length
        public TValueStrategy unsafeValues
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _values;
        }

        /// <summary>
        /// Note: the NativeStrategy implementations always hold a pre-boxed version of the buffer, so boxing
        /// never happens at run time. Unboxing does happen at runtime, but it's very cheap and never incurs
        /// allocations
        /// </summary>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IBuffer<TValue> UnsafeGetValues(out uint count)
        {
            count = _freeValueCellIndex;

            return _values.ToBuffer();
        }

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (int)_freeValueCellIndex;
        }

        public bool isValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buckets.isValid;
        }

        public SveltoDictionaryKeyEnumerable keys => new SveltoDictionaryKeyEnumerable(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //note, this returns readonly because the enumerator cannot be, but at the same time, it cannot be modified
        public SveltoDictionaryKeyValueEnumerator
                GetEnumerator()
        {
            return new SveltoDictionaryKeyValueEnumerator(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, in TValue value)
        {
            var itemAdded = AddValue(key, out var index);

#if DEBUG && !PROFILE_SVELTO
            if (itemAdded == false)
                throw new SveltoDictionaryException("Key already present");
#endif
            _values[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, in TValue value, out uint index)
        {
            var itemAdded = AddValue(key, out index);

            if (itemAdded == true)
                _values[index] = value;

            return itemAdded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, in TValue value)
        {
            var itemAdded = AddValue(key, out var index);

#if DEBUG && !PROFILE_SVELTO
            if (itemAdded == true)
                throw new SveltoDictionaryException("trying to set a value on a not existing key");
#endif

            _values[index] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Recycle()
        {
            if (_freeValueCellIndex == 0)
                return;

            _freeValueCellIndex = 0;
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif

            //Buckets cannot be FastCleared because it's important that the values are reset to 0
            _buckets.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (_freeValueCellIndex == 0)
                return;

            _freeValueCellIndex = 0;
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif

            //Buckets cannot be FastCleared because it's important that the values are reset to 0
            _buckets.Clear();

            _values.FastClear();
            _valuesInfo.FastClear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //WARNING this method must stay stateless (not relying on states that can change, it's ok to read 
        //constant states) because it will be used in multithreaded parallel code
        public bool ContainsKey(TKey key)
        {
            return TryFindIndex(key, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //WARNING this method must stay stateless (not relying on states that can change, it's ok to read 
        //constant states) because it will be used in multithreaded parallel code
        public bool TryGetValue(TKey key, out TValue result)
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                result = _values[(int)findIndex];
                return true;
            }

            result = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key)
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                return ref _values[(int)findIndex];
            }

            AddValue(key, out findIndex);

            _values[(int)findIndex] = default;

            return ref _values[(int)findIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key, Func<TValue> builder)
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                return ref _values[(int)findIndex];
            }

            AddValue(key, out findIndex);

            _values[(int)findIndex] = builder();

            return ref _values[(int)findIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key, out uint index)
        {
            if (TryFindIndex(key, out index) == true)
            {
                return ref _values[(int)index];
            }

            AddValue(key, out index);

            return ref _values[(int)index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd<W>(TKey key, FuncRef<W, TValue> builder, ref W parameter)
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                return ref _values[(int)findIndex];
            }

            AddValue(key, out findIndex);

            _values[(int)findIndex] = builder(ref parameter);

            return ref _values[(int)findIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue RecycleOrAdd<TValueProxy>(TKey key, Func<TValueProxy> builder
          , ActionRef<TValueProxy> recycler) where TValueProxy : class, TValue
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                return ref _values[(int)findIndex];
            }

            AddValue(key, out findIndex);

            if (_values[(int)findIndex] == null)
                _values[(int)findIndex] = builder();
            else
                recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[(int)findIndex]));

            return ref _values[(int)findIndex];
        }

        /// <summary>
        /// RecycledOrCreate makes sense to use on dictionaries that are fast cleared and use objects
        /// as value. Once the dictionary is fast cleared, it will try to reuse object values that are
        /// recycled during the fast clearing.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="builder"></param>
        /// <param name="recycler"></param>
        /// <param name="parameter"></param>
        /// <typeparam name="TValueProxy"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue RecycleOrAdd<TValueProxy, W>(TKey key, FuncRef<W, TValue> builder
          , ActionRef<TValueProxy, W> recycler, ref W parameter)
                where TValueProxy : class, TValue
        {
            if (TryFindIndex(key, out var findIndex) == true)
            {
                return ref _values[(int)findIndex];
            }

            AddValue(key, out findIndex);

            if (_values[(int)findIndex] == null)
                _values[(int)findIndex] = builder(ref parameter);
            else
                recycler(ref Unsafe.As<TValue, TValueProxy>(ref _values[(int)findIndex]), ref parameter);

            return ref _values[(int)findIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        //WARNING this method must stay stateless (not relying on states that can change, it's ok to read 
        //constant states) because it will be used in multi-threaded parallel code
        public ref TValue GetDirectValueByRef(uint index)
        {
            return ref _values[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetValueByRef(TKey key)
        {
#if DEBUG && !PROFILE_SVELTO
            if (TryFindIndex(key, out var findIndex) == true)
                return ref _values[(int)findIndex];

            throw new SveltoDictionaryException("Key not found");
#else
            //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
            TryFindIndex(key, out var findIndex);

            return ref _values[(int)findIndex];
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(uint size)
        {
            if (_values.capacity < size)
            {
                var expandPrime = HashHelpers.Expand((int)size);

                _values.Resize((uint)expandPrime, true, false);
                _valuesInfo.Resize((uint)expandPrime, true, true);
#if DEBUG && !PROFILE_SVELTO
                _structuralRevision.Increment();
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseCapacityBy(uint size)
        {
            var expandPrime = HashHelpers.Expand((int)_values.capacity + (int)size);

            _values.Resize((uint)expandPrime, true, false);
            _valuesInfo.Resize((uint)expandPrime, true, true);
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _values[(int)GetIndex(key)];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                AddValue(key, out var index);

                _values[index] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            return Remove(key, out _, out _);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key, out uint index, out TValue value)
        {
            int hash = key.GetHashCode();
            uint bucketIndex = Reduce((uint)hash, (uint)_buckets.capacity, _fastModBucketsMultiplier);

            //find the bucket
            int indexToValueToRemove = _buckets[bucketIndex] - 1;
            int itemAfterCurrentOne = -1;

            //Part one: look for the actual key in the bucket list if found I update the bucket list so that it doesn't
            //point anymore to the cell to remove
            while (indexToValueToRemove != -1)
            {
                ref var dictionaryNode = ref _valuesInfo[indexToValueToRemove];
                if (dictionaryNode.hashcode == hash && dictionaryNode.key.Equals(key) == true)
                {
                    //if the key is found and the bucket points directly to the node to remove
                    if (_buckets[bucketIndex] - 1 == indexToValueToRemove)
                    {
                        //the bucket will point to the previous cell. if a previous cell exists
                        //its next pointer must be updated!
                        //<--- iteration order  
                        //                      Bucket points always to the last one
                        //   ------- ------- -------
                        //   |  1  | |  2  | |  3  | //bucket cannot have next, only previous
                        //   ------- ------- -------
                        //--> insert order
                        _buckets[bucketIndex] = dictionaryNode.previous + 1;
                    }
                    else //we need to update the previous pointer if it's not the last element that is removed
                    {
                        DBC.Common.Check.Assert(itemAfterCurrentOne != -1, "this should never happen");
                        //update the previous pointer of the item after the one to remove with the previous pointer of the item to remove
                        _valuesInfo[itemAfterCurrentOne].previous = dictionaryNode.previous;
                    }

                    break; //don't miss this, at this point it must break and not update indexToValueToRemove
                }

                //a bucket always points to the last element of the list, so if the item is not found we need to iterate backward
                itemAfterCurrentOne = indexToValueToRemove;
                indexToValueToRemove = dictionaryNode.previous;
            }

            if (indexToValueToRemove == -1)
            {
                index = default;
                value = default;
                return false; //not found!
            }

            index = (uint)indexToValueToRemove; //index is a out variable, for internal use we want to know the index of the element to remove

            _freeValueCellIndex--; //one less value to iterate
            value = _values[indexToValueToRemove]; //value is a out variable, we want to know the value of the element to remove

            //Part two:
            //At this point nodes pointers and buckets are updated, but the _values array
            //still has got the value to delete. Remember the goal of this dictionary is to be able
            //to iterate over the values like an array, so the values array must always be up to date

            //if the cell to remove is the last one in the list, we can perform less operations (no swapping needed)
            //otherwise we want to move the last value cell over the value to remove

            var lastValueCellIndex = _freeValueCellIndex;
            if (indexToValueToRemove != lastValueCellIndex)
            {
                //we can transfer the last value of both arrays to the index of the value to remove.
                //in order to do so, we need to be sure that the bucket pointer is updated.
                //first we find the index in the bucket list of the pointer that points to the cell
                //to move
                ref var dictionaryNodeToMove = ref _valuesInfo[lastValueCellIndex];

                var movingBucketIndex = Reduce((uint)dictionaryNodeToMove.hashcode
                  , (uint)_buckets.capacity
                  , _fastModBucketsMultiplier);

                var linkedListIterationIndex = _buckets[movingBucketIndex] - 1;

                //if the key is found and the bucket points directly to the node to remove
                //it must now point to the cell where it's going to be moved (update bucket list first linked list node to iterate from)
                if (linkedListIterationIndex == lastValueCellIndex)
                    _buckets[movingBucketIndex] = indexToValueToRemove + 1;

                //find the prev element of the last element in the valuesInfo array
                while (_valuesInfo[linkedListIterationIndex].previous != -1 && _valuesInfo[linkedListIterationIndex].previous != lastValueCellIndex)
                    linkedListIterationIndex = _valuesInfo[linkedListIterationIndex].previous;

                //if we find any value that has the last value cell as previous, we need to update it to point to the new value index that is going to be replaced
                if (_valuesInfo[linkedListIterationIndex].previous != -1)
                    _valuesInfo[linkedListIterationIndex].previous = indexToValueToRemove;

                //finally, actually move the values
                _valuesInfo[indexToValueToRemove] = dictionaryNodeToMove;
                _values[indexToValueToRemove] = _values[lastValueCellIndex];
            }

#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trim()
        {
            if (_values.capacity == _freeValueCellIndex)
                return;

            _values.Resize(_freeValueCellIndex);
            _valuesInfo.Resize(_freeValueCellIndex);
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif
        }

        //I store all the index with an offset + 1, so that in the bucket list 0 means actually not existing.
        //When read the offset must be offset by -1 again to be the real one. In this way
        //I avoid to initialize the array to -1

        //WARNING this method must stay stateless (not relying on states that can change, it's ok to read 
        //constant states) because it will be used in multithreaded parallel code
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TKey key, out uint findIndex)
        {
            DBC.Common.Check.Require(_buckets.capacity > 0, "Dictionary arrays are not correctly initialized (0 size)");
            DBC.Common.Check.Require(_buckets.isValid  == true, "Dictionary arrays are not correctly initialized (not allocated)");

            int hash = key.GetHashCode();

            uint bucketIndex = Reduce((uint)hash, (uint)_buckets.capacity, _fastModBucketsMultiplier);

#if (DEBUG && !PROFILE_SVELTO)
            if (bucketIndex >= _buckets.capacity)
            {
                var error =
                        $"out of bounds bucket index {bucketIndex} - capacity {_buckets.capacity} - key {key} - hash {hash} - fastMod {_fastModBucketsMultiplier}";
                throw new DBC.Common.PreconditionException(error);
            }
#endif

            int valueIndex = _buckets[bucketIndex] - 1;

            //even if we found an existing value we need to be sure it's the one we requested
            while (valueIndex != -1)
            {
                //Comparer<TKey>.default needs to create a new comparer, so it is much slower
                //than assuming that Equals is implemented through IEquatable
                DBC.Common.Check.Require(valueIndex < _valuesInfo.capacity, "out of bounds value index");

                ref var dictionaryNode = ref _valuesInfo[valueIndex];
                if (dictionaryNode.hashcode == hash && dictionaryNode.key.Equals(key) == true)
                {
                    //this is the one
                    findIndex = (uint)valueIndex;
                    return true;
                }

                valueIndex = dictionaryNode.previous;
            }

            findIndex = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndex(TKey key)
        {
#if DEBUG && !PROFILE_SVELTO
            if (TryFindIndex(key, out var findIndex) == true)
                return findIndex;

            throw new SveltoDictionaryException("Key not found");
#else
            //Burst is not able to vectorise code if throw is found, regardless if it's actually ever thrown
            TryFindIndex(key, out var findIndex);

            return findIndex;
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Intersect<OTValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy>(
            SveltoDictionary<TKey, OTValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy> otherDicKeys)
                where OTKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
                where OTValueStrategy : struct, IBufferStrategy<OTValue>
                where OTBucketStrategy : struct, IBufferStrategy<int>
        {
            for (int i = count - 1; i >= 0; i--)
            {
                var tKey = unsafeKeys[i].key;
                if (otherDicKeys.ContainsKey(tKey) == false)
                    Remove(tKey);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Exclude<OTValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy>(
            SveltoDictionary<TKey, OTValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy> otherDicKeys)
                where OTKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
                where OTValueStrategy : struct, IBufferStrategy<OTValue>
                where OTBucketStrategy : struct, IBufferStrategy<int>
        {
            for (int i = count - 1; i >= 0; i--)
            {
                var tKey = unsafeKeys[i].key;
                if (otherDicKeys.ContainsKey(tKey) == true)
                    Remove(tKey);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Union<OTKeyStrategy, OTValueStrategy, OTBucketStrategy>(
            SveltoDictionary<TKey, TValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy> otherDicKeys)
                where OTKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
                where OTValueStrategy : struct, IBufferStrategy<TValue>
                where OTBucketStrategy : struct, IBufferStrategy<int>
        {
            foreach (var other in otherDicKeys)
            {
                this[other.key] = other.value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyFrom<OTKeyStrategy, OTValueStrategy, OTBucketStrategy>(
            SveltoDictionary<TKey, TValue, OTKeyStrategy, OTValueStrategy, OTBucketStrategy> otherDicKeys)
                where OTKeyStrategy : struct, IBufferStrategy<SveltoDictionaryNode<TKey>>
                where OTValueStrategy : struct, IBufferStrategy<TValue>
                where OTBucketStrategy : struct, IBufferStrategy<int>
        {
            _valuesInfo.SerialiseFrom(otherDicKeys._valuesInfo.AsBytesPointer());
            _values.SerialiseFrom(otherDicKeys._values.AsBytesPointer());
            _buckets.SerialiseFrom(otherDicKeys._buckets.AsBytesPointer());

            this._collisions = otherDicKeys._collisions;
            this._freeValueCellIndex = otherDicKeys._freeValueCellIndex;
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool AddValue(TKey key, out uint indexSet)
        {
            int hash = key.GetHashCode(); //IEquatable doesn't enforce the override of GetHashCode
            uint bucketIndex = Reduce((uint)hash, (uint)_buckets.capacity, _fastModBucketsMultiplier);

            //buckets value -1 means it's empty
            var valueIndex = _buckets[bucketIndex] - 1;

            if (valueIndex == -1)
            {
                ResizeIfNeeded();
                //create the info node at the last position and fill it with the relevant information
                _valuesInfo[_freeValueCellIndex] = new SveltoDictionaryNode<TKey>(key, hash);
            }
            else //collision or already exists
            {
                int currentValueIndex = valueIndex;
                do
                {
                    //must check if the key already exists in the dictionary
                    //Comparer<TKey>.default needs to create a new comparer, so it is much slower
                    //than assuming that Equals is implemented through IEquatable (but what if the comparer is statically cached?)
                    ref var dictionaryNode = ref _valuesInfo[currentValueIndex];
                    if (dictionaryNode.hashcode == hash && dictionaryNode.key.Equals(key) == true)
                    {
                        //the key already exists, simply replace the value!
                        indexSet = (uint)currentValueIndex;
                        return false;
                    }

                    currentValueIndex = dictionaryNode.previous;
                } while (currentValueIndex != -1); //-1 means no more values with key with the same hash

                ResizeIfNeeded();

                //oops collision!
                _collisions++;
                //create a new node which previous index points to node currently pointed in the bucket (valueIndex)
                //_freeValueCellIndex = valueIndex + 1
                _valuesInfo[_freeValueCellIndex] = new SveltoDictionaryNode<TKey>(key, hash, valueIndex);
                //Important: the new node is always the one that will be pointed by the bucket cell
                //so I can assume that the one pointed by the bucket is always the last value added
            }

            //item with this bucketIndex will point to the last value created
            _buckets[bucketIndex] = (int)(_freeValueCellIndex + 1);

            indexSet = _freeValueCellIndex;
            _freeValueCellIndex++;
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Increment();
#endif

            //too many collisions
            if (_collisions > _buckets.capacity)
            {
                if (_buckets.capacity < 100)
                    RecomputeBuckets((uint)((int)_collisions << 1));
                else
                    RecomputeBuckets((uint)HashHelpers.Expand((int)_collisions));
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void RecomputeBuckets(uint newSize)
        {
            //we need more space and less collisions
            _buckets.Resize(newSize, false, true);
            _collisions = 0;
            _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)_buckets.capacity);
            var bucketsCapacity = (uint)_buckets.capacity;

            //we need to get all the hash code of all the values stored so far and spread them over the new bucket
            //length
            var freeValueCellIndex = _freeValueCellIndex;
            for (int newValueIndex = 0; newValueIndex < freeValueCellIndex; ++newValueIndex)
            {
                //get the original hash code and find the new bucketIndex due to the new length
                ref var valueInfoNode = ref _valuesInfo[newValueIndex];
                var bucketIndex = Reduce((uint)valueInfoNode.hashcode, bucketsCapacity, _fastModBucketsMultiplier);
                //bucketsIndex can be -1 or a next value. If it's -1 means no collisions. If there is collision,
                //we create a new node which prev points to the old one. Old one next points to the new one.
                //the bucket will now points to the new one
                //In this way we can rebuild the linkedlist.
                //get the current valueIndex, it's -1 if no collision happens
                int existingValueIndex = _buckets[bucketIndex] - 1;
                //update the bucket index to the index of the current item that share the bucketIndex
                //(last found is always the one in the bucket)
                _buckets[bucketIndex] = newValueIndex + 1;
                if (existingValueIndex == -1)
                {
                    //ok nothing was indexed, the bucket was empty. We need to update the previous
                    //values of next and previous
                    valueInfoNode.previous = -1;
                }
                else
                {
                    //oops a value was already being pointed by this cell in the new bucket list,
                    //it means there is a collision, problem
                    _collisions++;
                    //the bucket will point to this value, so 
                    //the previous index will be used as previous for the new value.
                    valueInfoNode.previous = existingValueIndex;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void ResizeIfNeeded()
        {
            if (_freeValueCellIndex == _values.capacity)
            {
                var expandPrime = HashHelpers.Expand((int)_freeValueCellIndex);

                _values.Resize((uint)expandPrime, true, false);
                _valuesInfo.Resize((uint)expandPrime, true, true);
            }
        }

        public void Dispose()
        {
#if DEBUG && !PROFILE_SVELTO
            _structuralRevision.Dispose();
#endif
            _valuesInfo.Dispose();
            _values.Dispose();
            _buckets.Dispose();
        }

        internal TKeyStrategy _valuesInfo;
        internal TValueStrategy _values;
#if DEBUG && !PROFILE_SVELTO
        int structuralRevision => _structuralRevision;
        SharedNativeInt _structuralRevision;
#endif
        TBucketStrategy _buckets;

        uint _freeValueCellIndex;
        uint _collisions;
        ulong _fastModBucketsMultiplier;

        static readonly bool Is64BitProcess = Environment.Is64BitProcess;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint Reduce(uint hashcode, uint N, ulong fastModBucketsMultiplier)
        {
            if (hashcode >= N) //is the condition return actually an optimization?
                return Is64BitProcess
                        ? HashHelpers.FastMod(hashcode, N, fastModBucketsMultiplier)
                        : hashcode % N;

            return hashcode;
        }

        public readonly struct SveltoDictionaryKeyEnumerable
        {
            readonly SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> _dic;

            public SveltoDictionaryKeyEnumerable(SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> dic)
            {
                _dic = dic;
            }

            public SveltoDictionaryKeyEnumerator GetEnumerator() => new SveltoDictionaryKeyEnumerator(_dic);
        }

        public struct SveltoDictionaryKeyEnumerator
        {
            public SveltoDictionaryKeyEnumerator(SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> dic): this()
            {
                _dic = dic;
                _index = -1;
                _count = dic.count;
#if DEBUG && !PROFILE_SVELTO
                _startRevision = dic.structuralRevision;
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
#if DEBUG && !PROFILE_SVELTO
                if (_startRevision != _dic.structuralRevision)
                    throw new SveltoDictionaryException("can't modify a dictionary during its iteration");
#endif
                if (_index < _count - 1)
                {
                    ++_index;
                    return true;
                }

                return false;
            }

            public TKey Current => _dic._valuesInfo[_index].key;

            readonly SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> _dic;
            readonly int _count;
#if DEBUG && !PROFILE_SVELTO
            readonly int _startRevision;
#endif

            int _index;
        }

        public struct SveltoDictionaryKeyValueEnumerator
        {
            public SveltoDictionaryKeyValueEnumerator(in SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> dic): this()
            {
                _dic = dic;
                _index = -1;
                _count = dic.count;
#if DEBUG && !PROFILE_SVELTO
                _startRevision = dic.structuralRevision;
#endif
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
#if DEBUG && !PROFILE_SVELTO
                if (_startRevision != _dic.structuralRevision)
                    throw new SveltoDictionaryException("can't modify a dictionary while it is iterated");
#endif
                if (_index < _count - 1)
                {
                    ++_index;
                    return true;
                }

                return false;
            }

            public KeyValuePairFast<TKey, TValue, TValueStrategy> Current =>
                    new KeyValuePairFast<TKey, TValue, TValueStrategy>(_dic._valuesInfo[_index].key, _dic._values, _index);

            SveltoDictionary<TKey, TValue, TKeyStrategy, TValueStrategy, TBucketStrategy> _dic;
#if DEBUG && !PROFILE_SVELTO
            int _startRevision;
#endif
            int _count;

            int _index;

            public void SetRange(uint startIndex, uint count)
            {
                _index = (int)startIndex - 1;
                _count = (int)count;
#if DEBUG && !PROFILE_SVELTO
                if (_count > _dic.count)
                    throw new SveltoDictionaryException("can't set a count greater than the starting one");
#endif
            }
        }
    }

    public class SveltoDictionaryException: Exception
    {
        public SveltoDictionaryException(string keyAlreadyExisting): base(keyAlreadyExisting) { }
    }
}
