using System;

/// <span class="code-SummaryComment"><summary></span>
/// Represents a weak reference, which references an object while still allowing
/// that object to be reclaimed by garbage collection.
/// <span class="code-SummaryComment"></summary></span>
/// <span class="code-SummaryComment"><typeparam name="T">The type of the object that is referenced.</typeparam></span>

namespace Svelto.DataStructures
{
    public  struct WeakReference<T>:IEquatable<T> where T : class
    {
        public bool     IsValid => _weakReference != null && Target != null && _weakReference.IsAlive == true;

        public T    Target  => (T)_weakReference.Target;
        public bool IsAlive => _weakReference.IsAlive;

        public WeakReference(T target)
        {
            _weakReference = new WeakReference(target);
        }
        
        public bool TryGetTarget(out T target)
        {
            if (IsValid == false)
            {
                target = null;
                return false;
            }
            target = Target;
            return true;
        }

        public int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }   

        public bool Equals(T other)
        {
            DBC.Common.Check.Require(IsAlive);
            return Target.Equals(other);
        }
        
        public void Dispose()
        {
            _weakReference = null;
        }

        WeakReference _weakReference;
    }
}
