using System.Collections.Generic;
using System.Threading;

namespace Svelto.DataStructures
{
    public static class FasterListPool<T>
    {
        static readonly ThreadLocal<Stack<FasterList<T>>> _threadStacks = new ThreadLocal<Stack<FasterList<T>>>(() => new Stack<FasterList<T>>());

        public static FasterList<T> Get()
        {
            var stack = _threadStacks.Value;
            if (stack.Count > 0)
                return stack.Pop();

            return new FasterList<T>();
        }

        public static void Release(FasterList<T> list)
        {
            if (list != null)
            {
                list.Clear();
                _threadStacks.Value.Push(list);
            }
        }
    }
}
