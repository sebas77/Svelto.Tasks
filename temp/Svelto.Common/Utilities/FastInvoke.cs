using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Svelto.Common;

namespace Svelto.Utilities
{
    public static class FastInvoke<T> where T:struct
    {
        public static FastInvokeActionCast<T> MakeSetter(FieldInfo field)
        {
            if (field.FieldType.IsInterfaceEx() == true && field.FieldType.IsValueTypeEx() == false)
            {
                int offset = MemoryUtilities.GetFieldOffset(field);
                return (ref T target, object o) =>
                {
                    ref T pointer = ref Unsafe.AddByteOffset(ref target, (IntPtr) offset);
                    Unsafe.WriteUnaligned(ref Unsafe.As<T,byte>(ref pointer), o);
                };
            }

            throw new ArgumentException("<color=teal>Svelto.ECS</color> unsupported field (must be an interface and a class)");
        }
    }
    
    public delegate void FastInvokeActionCast<T>(ref T target, object value);
}
