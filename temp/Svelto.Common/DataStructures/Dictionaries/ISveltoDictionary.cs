using System;

namespace Svelto.DataStructures
{
    public interface IReadOnlySveltoDictionary<TKey, TValue>
    {
        int count { get; }
        bool ContainsKey(TKey key);
        bool TryGetValue(TKey key, out TValue result);
        bool TryFindIndex(TKey key, out uint findIndex);
        uint GetIndex(TKey key);
        void Dispose();
    }

    public interface ISveltoDictionary<TKey, TValue> : IReadOnlySveltoDictionary<TKey, TValue>
    {
        void Add(TKey key, in TValue value);
        void Set(TKey key, in TValue value);
        void Clear();
        ref TValue GetOrAdd(TKey key);
        ref TValue GetOrAdd(TKey key, Func<TValue> builder);
        ref TValue GetDirectValueByRef(uint index);
        ref TValue GetValueByRef(TKey key);
        void EnsureCapacity(uint size);
        void IncreaseCapacityBy(uint size);
        TValue this[TKey key] { get; set; }
        bool Remove(TKey key);
        void Trim();
    }
}
