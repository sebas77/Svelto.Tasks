using System.Runtime.CompilerServices;
using System.Threading;

namespace Svelto.DataStructures
{
#if !UNITY_WEBGL    
    public struct ReaderWriterLockSlimEx
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterReadLock()           { _slimLock.EnterReadLock(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitReadLock()            { _slimLock.ExitReadLock(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterUpgradableReadLock() { _slimLock.EnterUpgradeableReadLock(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitUpgradableReadLock()  { _slimLock.ExitUpgradeableReadLock(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterWriteLock()          { _slimLock.EnterWriteLock(); }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitWriteLock()           { _slimLock.ExitWriteLock(); }

        ReaderWriterLockSlim _slimLock;

        public static ReaderWriterLockSlimEx Create()
        {
            return new ReaderWriterLockSlimEx() {_slimLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion)};
        }
    }
#else
    //NO MT IN WEBGL
    public struct ReaderWriterLockSlimEx
    {
        public void EnterReadLock() {  }
        public void ExitReadLock()  {  }
        
        public void EnterUpgradableReadLock() {  }
        public void ExitUpgradableReadLock()  { }
        public void EnterWriteLock()          { }
        public void ExitWriteLock()           {; }
        
        public static ReaderWriterLockSlimEx Create() { return new ReaderWriterLockSlimEx(); }
    }
#endif
}