using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures.Native
{
    public ref struct LocalSveltoDictionaryNative<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey> where TValue : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator LocalSveltoDictionaryNative<TKey, TValue>
        (SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>, NativeStrategy<TValue>,
             NativeStrategy<int>> dic)
        {
            return new LocalSveltoDictionaryNative<TKey, TValue>()
            {
                _dictionary = dic
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator
            SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>, NativeStrategy<TValue>,
                NativeStrategy<int>>(LocalSveltoDictionaryNative<TKey, TValue> dic)
        {
            return dic._dictionary;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NB<TValue> GetValues(out uint count)
        {
            count = (uint)this.count;
            return _dictionary._values.ToRealBuffer();
        }

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(TKey key, in TValue value) { _dictionary.Add(key, in value); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(TKey key, in TValue value) { _dictionary.Set(key, in value); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() { _dictionary.Clear(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key) { return _dictionary.ContainsKey(key); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue result) { return _dictionary.TryGetValue(key, out result); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key) { return ref _dictionary.GetOrAdd(key); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetOrAdd(TKey key, Func<TValue> builder)
        {
            return ref _dictionary.GetOrAdd(key, builder);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetDirectValueByRef(uint index) { return ref _dictionary.GetDirectValueByRef(index); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TValue GetValueByRef(TKey key) { return ref _dictionary.GetValueByRef(key); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureCapacity(uint size)
        {
            _dictionary.EnsureCapacity(size);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void IncreaseCapacityBy(uint size)
        {
            _dictionary.IncreaseCapacityBy(size);
        }

        public TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary[key];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _dictionary[key] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Remove(TKey key) { return _dictionary.Remove(key); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Trim() { _dictionary.Trim(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TKey key, out uint findIndex) { return _dictionary.TryFindIndex(key, out findIndex); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndex(TKey key) { return _dictionary.GetIndex(key); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() { _dictionary.Dispose(); }

        SveltoDictionary<TKey, TValue, NativeStrategy<SveltoDictionaryNode<TKey>>, NativeStrategy<TValue>,
            NativeStrategy<int>> _dictionary;
    }
}