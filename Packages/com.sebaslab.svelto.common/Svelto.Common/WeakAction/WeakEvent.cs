using System;
using System.Collections.Generic;

// ────────────────────────────────────────────────────────────────────────────────
// WeakEvent (no-arg) – supports += / -= with Action
// ────────────────────────────────────────────────────────────────────────────────

public sealed class WeakEvent
{
    readonly List<WeakAction> list = new List<WeakAction>();

    public static WeakEvent operator +(WeakEvent e, Action h)
    {
        if (e == null) e = new WeakEvent();
        if (h != null) e.list.Add(new WeakAction(h));
        return e;
    }

    public static WeakEvent operator -(WeakEvent e, Action h)
    {
        if (e == null) return null;
        if (h == null) return e;
        for (int i = e.list.Count - 1; i >= 0; i--)
        {
            if (e.list[i].Matches(h))
            {
                e.list.RemoveAt(i);
                break;
            }
        }
        return e;
    }

    public void Invoke()
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            var w = list[i];
            if (w.IsAlive == false) { list.RemoveAt(i); continue; }
            w.Invoke();
        }
    }
}

// ────────────────────────────────────────────────────────────────────────────────
// WeakEvent<T> – supports += / -= with Action<T>
// ────────────────────────────────────────────────────────────────────────────────
public sealed class WeakEvent<T>
{
    readonly List<WeakAction<T>> weakActionsList = new List<WeakAction<T>>();

    public static WeakEvent<T> operator +(WeakEvent<T> e, Action<T> h)
    {
        if (e == null) e = new WeakEvent<T>();
        if (h != null) e.weakActionsList.Add(new WeakAction<T>(h));
        return e;
    }

    public static WeakEvent<T> operator -(WeakEvent<T> e, Action<T> h)
    {
        if (e == null) return null;
        if (h == null) return e;
        for (int i = e.weakActionsList.Count - 1; i >= 0; i--)
        {
            if (e.weakActionsList[i].Matches(h))
            {
                e.weakActionsList.RemoveAt(i);
                break;
            }
        }
        return e;
    }

    public void Invoke(T arg)
    {
        for (int i = weakActionsList.Count - 1; i >= 0; i--)
        {
            var w = weakActionsList[i];
            if (w.IsAlive == false) { weakActionsList.RemoveAt(i); continue; }
            w.Invoke(arg);
        }
    }
}
