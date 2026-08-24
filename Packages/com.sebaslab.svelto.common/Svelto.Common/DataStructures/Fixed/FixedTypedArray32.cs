#if NEW_C_SHARP || !UNITY_5_3_OR_NEWER
using System.Runtime.CompilerServices;

public struct FixedTypedArray32<T> where T : unmanaged
{
    static readonly int Capacity = 32;

#pragma warning disable CS0169
    FixedTypedArray16<T> sixteensA;
    FixedTypedArray16<T> sixteensB;
#pragma warning restore CS0169

    public int capacity => Capacity;

    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            DBC.Common.Check.Require(index < Capacity, "out of bound index");

            return Unsafe.Add(ref Unsafe.As<FixedTypedArray32<T>, T>(ref this), index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            DBC.Common.Check.Require(index < Capacity, "out of bound index");

            Unsafe.Add(ref Unsafe.As<FixedTypedArray32<T>, T>(ref this), index) = value;
        }
    }
}
#endif