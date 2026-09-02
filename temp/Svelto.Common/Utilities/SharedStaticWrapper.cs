using System.Runtime.CompilerServices;

namespace Svelto.Common
{
    //Note: SharedStatic MUST always be initialised outside burst otherwise undefined behaviour will happen
    public struct SharedStaticWrapper<T, Key> where T : unmanaged
    {
#if UNITY_BURST
        static readonly Unity.Burst.SharedStatic<T> uniqueContextID = Unity.Burst.SharedStatic<T>.GetOrCreate<Key>();

        public ref T Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref uniqueContextID.Data;
        }
#else
        static T uniqueContextID;
        
        public ref T Data
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref uniqueContextID;
        }
#endif
    }
}