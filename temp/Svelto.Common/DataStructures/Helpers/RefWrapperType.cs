using System;
using System.Diagnostics;

namespace Svelto.DataStructures
{
    public static class TypeRefWrapper<T>
    {
        public static RefWrapperType wrapper = new RefWrapperType(typeof(T));        
    }

    public readonly struct NativeRefWrapperType: IEquatable<NativeRefWrapperType>
    {
        static readonly FasterDictionary<RefWrapperType, System.Guid> GUIDCache =
                new FasterDictionary<RefWrapperType, System.Guid>();
        
        public NativeRefWrapperType(RefWrapperType type)
        {
            _typeGUID = GUIDCache.GetOrAdd(type, NewGuid);
            _hashCode = type.GetHashCode();
        }

        public bool Equals(NativeRefWrapperType other)
        {
            return _typeGUID == other._typeGUID;
        }
        
        public override int GetHashCode()
        {
            return _hashCode;
        }
        
        readonly        Guid       _typeGUID;
        readonly        int        _hashCode;
        static readonly Func<Guid> NewGuid;

        static NativeRefWrapperType()
        {
            NewGuid = System.Guid.NewGuid;
        }
    }
    
    [DebuggerDisplay("{_type}")]
    public readonly struct RefWrapperType: IEquatable<RefWrapperType> 
    {
        public RefWrapperType(Type type)
        {
            _type     = type;
        }

        public bool Equals(RefWrapperType other)
        {
            return _type == other._type;
        }
        
        public override int GetHashCode()
        {
            return _type.GetHashCode();
        }
        
        public static implicit operator Type(RefWrapperType t) => t._type;

        readonly          Type _type;
    }
    
    [DebuggerDisplay("{_string}")]
    public readonly struct RefWrapperString: IEquatable<RefWrapperString> 
    {
        public RefWrapperString(string @string)
        {
            _string     = @string;
        }

        public bool Equals(RefWrapperString other)
        {
            return _string == other._string;
        }
        
        public override int GetHashCode()
        {
            return _string.GetHashCode();
        }
        
        public static implicit operator string(RefWrapperString t) => t._string;
        public static implicit operator RefWrapperString(string t) => new RefWrapperString(t);

        readonly          string _string;
    }
}