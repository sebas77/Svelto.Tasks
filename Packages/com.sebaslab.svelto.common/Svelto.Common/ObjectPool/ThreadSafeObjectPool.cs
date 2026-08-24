using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Svelto.DataStructures;
#if DEBUG && !PROFILE_SVELTO
using System.Collections.Generic;
#endif

namespace Svelto.ObjectPool
{
    public class ThreadSafeObjectPool<T> : IObjectPool<T>, IDisposable
#if DEBUG && !PROFILE_SVELTO
                       , IObjectPoolDebug
#endif
        where T : class
    {
        public ThreadSafeObjectPool(Func<T> objectFactoryFunc) : base()
        {
            _objectFactoryFunc = objectFactoryFunc;
        }

        public void Preallocate(int pool, int size, Func<T> onFirstUse)
        {
            for (int i = size - 1; i >= 0; --i)
                Preallocate(pool, onFirstUse);
        }
        
        public async Task Preallocate(int pool, int size, Func<Task<T>> onFirstUse)
        {
            for (int i = size - 1; i >= 0; --i)
                await Preallocate(pool, onFirstUse);
        }

        /// <summary>
        /// Create Or Reuse
        /// </summary>
        public T Get(int pool, Func<T> onFirstUse)
        {
            return CreateOrReuse(pool, onFirstUse);
        }
        
        /// <summary>
        /// Create Or Reuse
        /// </summary>
        public async Task<T> Use(int pool, Func<Task<T>> onFirstUse)
        {
            return await CreateOrReuse(pool, onFirstUse);
        }

        /// <summary>
        /// Create Or Reuse
        /// </summary>
        public T Use(Func<T> onFirstUse)
        {
            return CreateOrReuse(0, onFirstUse);
        }

        /// <summary>
        /// Create Or Reuse
        /// </summary>
        public T Use(int pool)
        {
            DBC.Common.Check.Require(_objectFactoryFunc != null, "You need to pass a function to create the object");

            return CreateOrReuse(pool, _objectFactoryFunc);
        }

        /// <summary>
        /// Create Or Reuse
        /// </summary>
        public T Use()
        {
            DBC.Common.Check.Require(_objectFactoryFunc != null, "You need to pass a function to create the object");

            return CreateOrReuse(0, _objectFactoryFunc);
        }
        
        /// <summary>
        /// Reuse if recycled
        /// </summary>
        public bool TryReuse(int pool, out T obj)
        {
            obj = null;

            ThreadSafeStack<T> localPool = ReturnValidPool(_recycledPools, pool);

            while (IsNull(obj) == true && localPool.count > 0)
                localPool.TryPop(out obj);

            if (IsNull(obj) == false)
            {
#if DEBUG && !PROFILE_SVELTO
                _alreadyRecycled.TryRemove(obj, out _);
#endif
                _objectsReused++;

                return true;
            }

            return false;
        }
        
        public virtual void Recycle(T go, int pool)
        {
            InternalRecycle(go, pool);
        }

        public virtual void Recycle(T go)
        {
            InternalRecycle(go, 0);
        }
        
        protected virtual void OnDispose()
        { }

        public void Dispose()
        {
            OnDispose();

            if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
                using (var recycledPoolsGetValues = _recycledPools.GetValues)
                {
                    var values = recycledPoolsGetValues.GetValues(out var count);
                    for (int i = 0; i < count; i++)                     
                    {
                        using (var stacks = values[i].GetValues)
                        {
                            var stackValues = stacks.GetValues();
                            foreach (var obj in stackValues)
                                ((IDisposable)obj).Dispose();
                        }
                    }
                }

            _recycledPools.Clear();

            _disposed = true;
        }

        public void Clear()
        {
            _recycledPools.Clear();
        }

        public int GetNumberOfObjectsCreatedSinceLastTime()
        {
            var ret = _objectsCreated;

            _objectsCreated = 0;

            return ret;
        }

        public int GetNumberOfObjectsReusedSinceLastTime()
        {
            var ret = _objectsReused;

            _objectsReused = 0;

            return ret;
        }

        public int GetNumberOfObjectsRecycledSinceLastTime()
        {
            var ret = _objectsRecycled;

            _objectsRecycled = 0;

            return ret;
        }

#if DEBUG && !PROFILE_SVELTO
        public List<ObjectPoolDebugStructure> DebugPoolInfo(List<ObjectPoolDebugStructure> debugInfo)
        {
            debugInfo.Clear();

            //todo put it back
            // for (var enumerator = _recycledPools.GetEnumerator(); enumerator.MoveNext();)
            // {
            //     var currentValue = enumerator.Current.Value;
            //     debugInfo.Add(new ObjectPoolDebugStructure(enumerator.Current.Key, currentValue.Count));
            // }

            return debugInfo;
        }
#endif

        protected T Preallocate(int pool, Func<T> onFirstInit)
        {
            var localPool = ReturnValidPool(_recycledPools, pool);

            var go = onFirstInit();

            localPool.Push(go);

            return go;
        }
        
        protected async Task<T> Preallocate(int pool, Func<Task<T>> onFirstInit)
        {
            var localPool = ReturnValidPool(_recycledPools, pool);

            var go = await onFirstInit();

            localPool.Push(go);

            return go;
        }
        
        void InternalRecycle(T obj, int pool)
        {
            if (_disposed)
                return;
#if DEBUG && !PROFILE_SVELTO
            if (_alreadyRecycled.TryAdd(obj, true) == false)
                throw new Exception("An object already Recycled in the pool has been Recycled again");
#endif
            DBC.Common.Check.Assert(_recycledPools.ContainsKey(pool) == true, "invalid pool requested");

            _recycledPools[pool].Push(obj);

            _objectsRecycled++;
        }

        ThreadSafeStack<T> ReturnValidPool(ThreadSafeDictionary<int, ThreadSafeStack<T>> pools, int pool)
        {
            if (pools.TryGetValue(pool, out var localPool) == false)
                pools[pool] = localPool = new ThreadSafeStack<T>();

            return localPool;
        }

        T CreateOrReuse(int pool, Func<T> onFirstInit)
        {
            if (TryReuse(pool, out T ret) == false)
            {
                ret = onFirstInit();

                _objectsCreated++;
            }

            return ret;
        }
        
        async Task<T> CreateOrReuse(int pool, Func<Task<T>> onFirstInit)
        {
            if (TryReuse(pool, out T ret) == false)
            {
                ret = await onFirstInit();

                _objectsCreated++;
            }

            return ret;
        }

        static bool IsNull(object aObj) //keep otherwise won't work with stupid stuff like unity GO do with equality
        {
            return aObj is null;
        }

        readonly ThreadSafeDictionary<int, ThreadSafeStack<T>> _recycledPools =
            new ThreadSafeDictionary<int, ThreadSafeStack<T>>();
        

        readonly Func<T> _objectFactoryFunc;
#if DEBUG && !PROFILE_SVELTO
        readonly ConcurrentDictionary<T, bool> _alreadyRecycled = new ConcurrentDictionary<T, bool>();
#endif

        int _objectsReused;
        int _objectsCreated;
        int _objectsRecycled;

        bool _disposed;
    }
}