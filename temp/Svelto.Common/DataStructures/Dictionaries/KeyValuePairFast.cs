using System;
using System.Diagnostics;

namespace Svelto.DataStructures
{
    /// <summary>
    ///the mechanism to use arrays is fundamental to work
    /// </summary>
    [DebuggerDisplay("[{key}] - {value}")]
    [DebuggerTypeProxy(typeof(KeyValuePairFastDebugProxy<,,>))]
    public readonly struct KeyValuePairFast<TKey, TValue, TValueStrategy> where TKey : struct, IEquatable<TKey>
            where TValueStrategy : struct,
            IBufferStrategy<TValue>
    {
        public KeyValuePairFast(in TKey key, in TValueStrategy dicValues, int index)
        {
            _dicValues = dicValues;
            _index = index;
            _key = key;
        }
    
        public void Deconstruct(out TKey key, out TValue value)
        {
            key = this.key;
            value = this.value;
        }
    
        public TKey key => _key;
        public ref TValue value => ref _dicValues[_index];
    
        readonly TValueStrategy _dicValues;
        readonly TKey _key;
        readonly int _index;
    }
    
    public sealed class KeyValuePairFastDebugProxy<TKey, TValue, TValueStrategy> where TKey : struct, IEquatable<TKey>
            where TValueStrategy : struct, IBufferStrategy<TValue>
    {
        public KeyValuePairFastDebugProxy(in KeyValuePairFast<TKey, TValue, TValueStrategy> keyValue)
        {
            this._keyValue = keyValue;
        }
    
        public TKey key => _keyValue.key;
        public TValue value => _keyValue.value;
    
        readonly KeyValuePairFast<TKey, TValue, TValueStrategy> _keyValue;
    }
}