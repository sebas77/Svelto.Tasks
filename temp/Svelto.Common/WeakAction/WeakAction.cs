using System;
using System.Reflection;


// ────────────────────────────────────────────────────────────────────────────────
// WeakAction (no-arg)
// ────────────────────────────────────────────────────────────────────────────────
public sealed class WeakAction
{
    readonly WeakReference<object> targetRef;   // null for static methods
    readonly MethodInfo method;
    readonly Action staticInvoke; // fast-path for static methods
    delegate void OpenAction(object target);
    readonly OpenAction openInvoke;

    public WeakAction(Action action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        method = action.Method;
        object target = action.Target;
        if (target == null)
        {
            staticInvoke = action;
            targetRef = null;
            openInvoke = null;
        }
        else
        {
            targetRef = new WeakReference<object>(target);
            openInvoke = (tgt) => method.Invoke(tgt, null);
        }
    }

    internal bool Matches(Action action)
    {
        if (action == null) return false;
        if (staticInvoke != null) return action == staticInvoke;

        object t;
        if (targetRef.TryGetTarget(out t) == false) return false;
        return t == action.Target && action.Method == method;
    }

    public bool IsAlive
    {
        get
        {
            if (staticInvoke != null) return true;
            object t;
            return targetRef.TryGetTarget(out t) && t != null;
        }
    }

    public void Invoke()
    {
        if (IsAlive == false) return;
        if (staticInvoke != null) { staticInvoke(); return; }

        object t;
        if (targetRef.TryGetTarget(out t) == false || t == null) return;
        openInvoke(t);
    }
}

// ────────────────────────────────────────────────────────────────────────────────
// WeakAction<T>
// ────────────────────────────────────────────────────────────────────────────────
public sealed class WeakAction<T>
{
    readonly WeakReference<object> targetRef;   // null for static methods
    readonly MethodInfo method;
    delegate void OpenAction(object target, T arg);
    readonly OpenAction openInvoke;
    readonly Action<T> staticInvoke;
    readonly object[] args; // reused to avoid per-call alloc

    public WeakAction(Action<T> action)
    {
        if (action == null) throw new ArgumentNullException(nameof(action));

        method = action.Method;
        object target = action.Target;

        if (target == null)
        {
            staticInvoke = action;
        }
        else
        {
            targetRef = new WeakReference<object>(target);
            args ??= new object[1];
            openInvoke = (tgt, arg) => { args[0] = arg; method.Invoke(tgt, args); };
        }
    }

    internal bool Matches(Action<T> action)
    {
        if (action == null) return false;
        if (staticInvoke != null) return action == staticInvoke;

        if (targetRef.TryGetTarget(out var t) == false) return false;
        return t == action.Target && action.Method == method;
    }

    public bool IsAlive
    {
        get
        {
            if (staticInvoke != null) return true;
            return targetRef.TryGetTarget(out var t) && t != null;
        }
    }

    public void Invoke(T arg)
    {
        if (IsAlive == false) return;

        if (staticInvoke != null)
        {
            staticInvoke(arg);
            return;
        }

        if (targetRef.TryGetTarget(out var t) == false || t == null) return;
        openInvoke(t, arg);
    }
}


