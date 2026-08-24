using System;

namespace Svelto.ObjectPool
{
    public interface IObjectPool<T>
    {
        void Recycle(T go, int    pool);

        void Preallocate(int    pool,     int size, Func<T> onFirstUse);

        T Get(int    pool,     Func<T> onFirstUse = null);

        void Clear();
    }
}