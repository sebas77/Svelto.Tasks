using System;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.DataStructures.Native
{
    /// <summary>
    /// This dictionary has been created for just one reason: I needed a dictionary that would have let me iterate
    /// over the values as an array, directly, without generating one or using an iterator.
    /// For this goal is N times faster than the standard dictionary. Faster dictionary is also faster than
    /// the standard dictionary for most of the operations, but the difference is negligible. The only slower operation
    /// is resizing the memory on add, as this implementation needs to use two separate arrays compared to the standard
    /// one
    /// note: use native memory? Use _valuesInfo only when there are collisions?
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public struct SharedSveltoDictionaryNative<TKey, TValue> : ISveltoDictionary<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey> where TValue : struct
    {
        public static SharedSveltoDictionaryNative<TKey, TValue> Create
            (Allocator allocatorStrategy = Allocator.Persistent)
        {
            return new SharedSveltoDictionaryNative<TKey, TValue>(0, allocatorStrategy);
        }

        public SharedSveltoDictionaryNative(uint size, Allocator allocatorStrategy = Allocator.Persistent)
        {
            var dictionary =
                new SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>, NativeStrategy<TValue>,
                    NativeStrategy<int>>(size, allocatorStrategy);

            _sharedDictionary =
                MemoryUtilities
                   .NativeAlloc<SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>,
                        NativeStrategy<TValue>, NativeStrategy<int>>>(1, allocatorStrategy);

            _allocatorStrategy = allocatorStrategy;

            this.dictionary = dictionary;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NB<TValue> GetValues(out uint count)
        {
            count = (uint)this.count;
            return dictionary._values.ToRealBuffer();
        }

        public NB<TValue> unsafeValues
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dictionary._values.ToRealBuffer();
        }

        public SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>,
            NativeStrategy<TValue>, NativeStrategy<int>>.SveltoDictionaryKeyValueEnumerator GetEnumerator() => dictionary.GetEnumerator();

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dictionary.count;
        }

        public bool isValid => _sharedDictionary != IntPtr.Zero && dictionary.isValid;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, in TValue value)
        {
            dictionary.Add(key, in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(TKey key, in TValue value, out uint index)
        {
            return dictionary.TryAdd(key, in value, out index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, in TValue value)
        {
            dictionary.Set(key, in value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            dictionary.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue result)
        {
            return dictionary.TryGetValue(key, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key)
        {
            return ref dictionary.GetOrAdd(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key, Func<TValue> builder)
        {
            return ref dictionary.GetOrAdd(key, builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetDirectValueByRef(uint index)
        {
            return ref dictionary.GetDirectValueByRef(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetValueByRef(TKey key)
        {
            return ref dictionary.GetValueByRef(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(uint size)
        {
            dictionary.EnsureCapacity(size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseCapacityBy(uint size)
        {
            dictionary.IncreaseCapacityBy(size);
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => dictionary[key];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => dictionary[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key)
        {
            return dictionary.Remove(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trim()
        {
            dictionary.Trim();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TKey key, out uint findIndex)
        {
            return dictionary.TryFindIndex(key, out findIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndex(TKey key)
        {
            return dictionary.GetIndex(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
#if DEBUG && !PROFILE_SVELTO
            if (_sharedDictionary == null)
                throw new Exception("SharedSveltoDictionary: try to dispose an already disposed array");
#endif
            dictionary.Dispose();

            MemoryUtilities.NativeFree(_sharedDictionary, _allocatorStrategy);
            _sharedDictionary = IntPtr.Zero;
        }

        public ref SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>, NativeStrategy<TValue>,
            NativeStrategy<int>> dictionary
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                unsafe
                {
                    return ref Unsafe
                       .AsRef<SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>,
                            NativeStrategy<TValue>, NativeStrategy<int>>>((void*)_sharedDictionary);
                }
            }
        }

        public bool isDisposed => _sharedDictionary == IntPtr.Zero;

#if UNITY_COLLECTIONS || UNITY_JOBS || UNITY_BURST
#if UNITY_BURST
        [Unity.Burst.NoAlias]
#endif
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction]
#endif
        IntPtr _sharedDictionary;

        readonly Allocator _allocatorStrategy;
    }
}
