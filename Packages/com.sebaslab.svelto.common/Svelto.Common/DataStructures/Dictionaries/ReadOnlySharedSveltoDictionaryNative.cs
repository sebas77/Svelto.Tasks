using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures.Native
{
    public readonly struct ReadonlySharedSveltoDictionaryNative<TKey, TValue> : global::Svelto.DataStructures.IReadOnlySveltoDictionary<TKey, TValue>
        where TKey : unmanaged, IEquatable<TKey> where TValue : struct
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ReadonlySharedSveltoDictionaryNative(SharedSveltoDictionaryNative<TKey, TValue> dic)
        {
            _dictionary = dic;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadonlySharedSveltoDictionaryNative<TKey, TValue>(in SharedSveltoDictionaryNative<TKey, TValue> dic)
        {
            return new ReadonlySharedSveltoDictionaryNative<TKey, TValue>(dic);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NB<TValue> GetValues(out uint count)
        {
            return _dictionary.GetValues(out count);
        }

        public int count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary.count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetValue(TKey key, out TValue result)
        {
            return _dictionary.TryGetValue(key, out result);
        }

        public ref TValue this[TKey key]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref _dictionary.GetValueByRef(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryFindIndex(TKey key, out uint findIndex)
        {
            return _dictionary.TryFindIndex(key, out findIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetIndex(TKey key)
        {
            return _dictionary.GetIndex(key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            _dictionary.Dispose();
        }
        
        public bool isValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _dictionary.isValid;
        }

        readonly SharedSveltoDictionaryNative<TKey, TValue> _dictionary;
    }
}
