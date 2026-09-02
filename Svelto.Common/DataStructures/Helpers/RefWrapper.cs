using System;
using System.Collections.Generic;

namespace Svelto.DataStructures
{
    public readonly struct RefWrapper<T>: IEquatable<RefWrapper<T>>, IEquatable<T> where T:class
    {
        public RefWrapper(T type)
        {
            _type    = type;
            _hashCode = type.GetHashCode();
        }

        public bool Equals(RefWrapper<T> other)
        {
            return _type.Equals(other._type);
        }

        public bool Equals(T other)
        {
            return _type.Equals(other);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public T type => _type;

        public static implicit operator T(RefWrapper<T> t) => t._type;
        public static implicit operator RefWrapper<T>(T t) => new RefWrapper<T>(t);

        readonly T   _type;
        readonly int _hashCode;
    }
    
    public readonly struct RefWrapper<T, Comparer>: IEquatable<RefWrapper<T, Comparer>>, IEquatable<T> 
        where T:class where Comparer: struct, IEqualityComparer<T>
    {
        public RefWrapper(T type)
        {
            _type    = type;
            _comparer = default;
        }

        public bool Equals(RefWrapper<T, Comparer> other)
        {
            return _comparer.Equals(this, other.type);
        }

        public bool Equals(T other)
        {
            return _comparer.Equals(type, other);
        }

        public override int GetHashCode()
        {
            return _comparer.GetHashCode(this);
        }

        public T type => _type;

        public static implicit operator T(RefWrapper<T, Comparer> t) => t.type;
        public static implicit operator RefWrapper<T, Comparer>(T t) => new RefWrapper<T, Comparer>(t);

        readonly T        _type;
        readonly Comparer _comparer;
    }
}