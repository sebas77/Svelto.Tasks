#if UNITY_5_3_OR_NEWER
using UnityEngine;
using UnityEngine.LowLevel;
using System;
using System.Collections.Generic;

//#if UNITY_EDITOR
//using UnityEditor;
//
//[InitializeOnLoad]
//static class ClearCustomPlayerLoopsOnStop
//{
//    static ClearCustomPlayerLoopsOnStop()
//    {
//        Action<PlayModeStateChange> handler = null;
//
//        handler = state =>
//        {
//            if (state == PlayModeStateChange.ExitingPlayMode)
//            {
//                try
//                {
//                    PlayerLoopUtility.ClearAllCustomSystems();
//                }
//                catch (Exception ex)
//                {
//                    Debug.LogError($"Error while clearing custom systems: {ex}");
//                }
//                finally
//                {
//                    EditorApplication.playModeStateChanged -= handler;
//                }
//            }
//            else
//            if (state == PlayModeStateChange.EnteredEditMode)
//            {
//                EditorApplication.playModeStateChanged -= handler;
//                EditorApplication.playModeStateChanged += handler;
//            }
//        };
//        
//        EditorApplication.playModeStateChanged += handler;
//    }
//}
//#endif

public static class PlayerLoopUtility
{
    // ---------------------------------------------------------------------
    // State
    // ---------------------------------------------------------------------

    static readonly HashSet<Type> _registeredCustomTypes = new();
    static readonly Dictionary<Type, int> _customSystemOffsets = new();

    // ---------------------------------------------------------------------
    // Public API – before / after
    // ---------------------------------------------------------------------

    public static bool AddSystemBefore<TExisting, TCustom>(
        PlayerLoopSystem.UpdateFunction update,
        bool scanChildren,
        int offset = 0)
        where TExisting : struct
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        var custom = CreateSystem(typeof(TCustom), update);

        var root = PlayerLoop.GetCurrentPlayerLoop();
        if (RemoveInternal(ref root, typeof(TCustom), scanChildren))
        {
            Debug.LogWarning($"----------> Removed existing system {typeof(TCustom)}");
            _registeredCustomTypes.Remove(typeof(TCustom));   
            _customSystemOffsets.Remove(typeof(TCustom));
        }

        if (InsertBefore(ref root, typeof(TExisting), custom, scanChildren, offset) == false)
        {
            Debug.LogError($"----------> Failed to insert system {typeof(TCustom)} before {typeof(TExisting)}");
            return false;
        }
        else
        {
            Debug.Log($"----------> Inserted system {typeof(TCustom)} before {typeof(TExisting)} with offset {offset}");
        }

        PlayerLoop.SetPlayerLoop(root);
        _registeredCustomTypes.Add(typeof(TCustom));
        _customSystemOffsets[typeof(TCustom)] = offset;
        return true;
    }

    public static bool AddSystemAfter<TExisting, TCustom>(
        PlayerLoopSystem.UpdateFunction update,
        bool scanChildren,
        int offset = 0)
        where TExisting : struct
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        var custom = CreateSystem(typeof(TCustom), update);

        var root = PlayerLoop.GetCurrentPlayerLoop();
        if (RemoveInternal(ref root, typeof(TCustom), scanChildren))
        {
            Debug.LogWarning($"----------> Removed existing system {typeof(TCustom)}");
            _registeredCustomTypes.Remove(typeof(TCustom));    
            _customSystemOffsets.Remove(typeof(TCustom));
        }

        if (InsertAfter(ref root, typeof(TExisting), custom, scanChildren, offset) == false)
        {
            Debug.LogError($"----------> Failed to insert system {typeof(TCustom)} after {typeof(TExisting)}");
            return false;
        }
        else
        {
            Debug.Log($"----------> Inserted system {typeof(TCustom)} after {typeof(TExisting)} with offset {offset}");
        }

        PlayerLoop.SetPlayerLoop(root);
        _registeredCustomTypes.Add(typeof(TCustom));
        _customSystemOffsets[typeof(TCustom)] = offset;
        return true;
    }

    public static bool RemoveSystem<TCustom>(bool scanChildren)
    {
        var root = PlayerLoop.GetCurrentPlayerLoop();
        if (RemoveInternal(ref root, typeof(TCustom), scanChildren) == false)
        {
            Debug.LogError($"----------> Failed to remove system {typeof(TCustom)}");
            return false;
        }
        else
        {
            Debug.Log($"----------> Removed system {typeof(TCustom)}");
        }

        PlayerLoop.SetPlayerLoop(root);
        _registeredCustomTypes.Remove(typeof(TCustom));
        _customSystemOffsets.Remove(typeof(TCustom));
        return true;
    }

    /// <summary>
    /// Remove a subsystem <typeparamref name="TTarget"/> that lives somewhere
    /// inside the <typeparamref name="TParent"/> branch of the player-loop.
    /// </summary>
    public static bool RemoveSystem<TParent, TTarget>()
        where TParent : struct
    {
        var root = PlayerLoop.GetCurrentPlayerLoop();
        var rootSubs = root.subSystemList;

        var parentType = typeof(TParent);
        var targetType = typeof(TTarget);
        var parentIndex = -1;

        // 1. locate the parent at root level ---------------------------------
        for (int i = 0; i < rootSubs.Length; i++)
        {
            if (rootSubs[i].type == parentType)
            {
                parentIndex = i;
                break;
            }
        }

        if (parentIndex < 0)
        {
            Debug.LogError($"----------> Failed to find system {typeof(TParent)}");
            return false;          // parent not found
        }

        // 2. work on a copy of the parent node -------------------------------
        var parentNode = rootSubs[parentIndex];

        if (RemoveInternal(ref parentNode, targetType, false) == false)
        {
            Debug.LogError($"----------> Failed to remove system {typeof(TTarget)} child of {typeof(TParent)}");
            
            return false;                           // target not found
        }
        else
        {
            Debug.Log($"----------> Removed system {typeof(TTarget)} child of {typeof(TParent)}");
        }

        // 3. write the mutated parent back and commit the loop ---------------
        rootSubs[parentIndex] = parentNode;
        root.subSystemList = rootSubs;
        PlayerLoop.SetPlayerLoop(root);

        _registeredCustomTypes.Remove(targetType);
        _customSystemOffsets.Remove(targetType);
        return true;
    }

    public static void ClearAllCustomSystems(bool scanChildren = true)
    {
        if (_registeredCustomTypes.Count == 0) return;

        var root = PlayerLoop.GetCurrentPlayerLoop();
        var changed = false;

        // copy to avoid mutation-while-iterating
        var toRemove = new List<Type>(_registeredCustomTypes);

        foreach (var t in toRemove)
            changed |= RemoveInternal(ref root, t, scanChildren);

        if (changed)
            PlayerLoop.SetPlayerLoop(root);

        _registeredCustomTypes.Clear();
        _customSystemOffsets.Clear();
    }

    // ---------------------------------------------------------------------
    // Public API – children insertion
    // ---------------------------------------------------------------------

    public static bool AddSystemAsFirstChild<TParent, TCustom>(
        PlayerLoopSystem.UpdateFunction update,
        bool scanChildren = true,
        int offset = 0)
        where TParent : struct
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        var custom = CreateSystem(typeof(TCustom), update);

        var root = PlayerLoop.GetCurrentPlayerLoop();
        if (RemoveInternal(ref root, typeof(TCustom), scanChildren))
        {
            Debug.LogWarning($"----------> Removed existing system {typeof(TCustom)}");
            _registeredCustomTypes.Remove(typeof(TCustom));
            _customSystemOffsets.Remove(typeof(TCustom));
        }

        if (InsertAsFirstChild(ref root, typeof(TParent), custom, scanChildren, offset) == false)
        {
            Debug.LogError($"----------> Failed to insert system {typeof(TCustom)} as first child of {typeof(TParent)}");
            return false;
        }
        else
        {
            Debug.Log($"----------> Inserted system {typeof(TCustom)} as first child of {typeof(TParent)} with offset {offset}");
        }

        PlayerLoop.SetPlayerLoop(root);
        _registeredCustomTypes.Add(typeof(TCustom));
        _customSystemOffsets[typeof(TCustom)] = offset;
        return true;
    }

    public static bool AddSystemAsLastChild<TParent, TCustom>(
        PlayerLoopSystem.UpdateFunction update,
        bool scanChildren,
        int offset = 0)
        where TParent : struct
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        var custom = CreateSystem(typeof(TCustom), update);

        var root = PlayerLoop.GetCurrentPlayerLoop();
        if (RemoveInternal(ref root, typeof(TCustom), scanChildren))
        {
            Debug.LogWarning($"----------> Removed existing system {typeof(TCustom)}");
            _registeredCustomTypes.Remove(typeof(TCustom));
            _customSystemOffsets.Remove(typeof(TCustom));
        }

        if (InsertAsLastChild(ref root, typeof(TParent), custom, scanChildren, offset) == false)
        {
            Debug.LogError($"----------> Failed to insert system {typeof(TCustom)} as last child of {typeof(TParent)}");
            return false;
        }
        else
        {
            Debug.Log($"----------> Inserted system {typeof(TCustom)} as last child of {typeof(TParent)} with offset {offset}");
        }

        PlayerLoop.SetPlayerLoop(root);
        _registeredCustomTypes.Add(typeof(TCustom));
        _customSystemOffsets[typeof(TCustom)] = offset;
        return true;
    }

    // ---------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------

    static PlayerLoopSystem CreateSystem(Type type, PlayerLoopSystem.UpdateFunction update)
        => new() { type = type, updateDelegate = update };

    // ----- insertion helpers (before / after) -----------------------------

    static bool InsertBefore(
        ref PlayerLoopSystem root,
        Type existing,
        in PlayerLoopSystem custom,
        bool scanChildren,
        int offset)
    {
        var subs = root.subSystemList;
        if (subs == null) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i].type == existing)
            {
                int insertIndex = i;
                // Scan backward from i-1 to 0
                for (int j = i - 1; j >= 0; j--)
                {
                    if (_registeredCustomTypes.Contains(subs[j].type)                          &&
                        _customSystemOffsets.TryGetValue(subs[j].type, out int existingOffset) &&
                        existingOffset <= offset)
                    {
                        insertIndex = j + 1;
                        break;
                    }
                }

                // Shift existing systems and insert
                var newArr = new PlayerLoopSystem[subs.Length + 1];
                Array.Copy(subs, 0, newArr, 0, insertIndex);
                newArr[insertIndex] = custom;
                Array.Copy(subs, insertIndex, newArr, insertIndex + 1, subs.Length - insertIndex);
                root.subSystemList = newArr;
                return true;
            }
        }

        if (scanChildren == false) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (InsertBefore(ref subs[i], existing, custom, true, offset))
            {
                root.subSystemList = subs;  // propagate child modifications
                return true;
            }
        }

        return false;
    }

    static bool InsertAfter(
        ref PlayerLoopSystem root,
        Type existing,
        in PlayerLoopSystem custom,
        bool scanChildren,
        int offset)
    {
        var subs = root.subSystemList;
        if (subs == null) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i].type == existing)
            {
                int insertIndex = i + 1;
                // Scan forward from i+1 to subs.Length
                for (int j = i + 1; j < subs.Length; j++)
                {
                    if (_registeredCustomTypes.Contains(subs[j].type)                          &&
                        _customSystemOffsets.TryGetValue(subs[j].type, out int existingOffset) &&
                        existingOffset >= offset)
                    {
                        insertIndex = j;
                        break;
                    }
                }

                // Insert and shift
                var newArr = new PlayerLoopSystem[subs.Length + 1];
                Array.Copy(subs, 0, newArr, 0, insertIndex);
                newArr[insertIndex] = custom;
                Array.Copy(subs, insertIndex, newArr, insertIndex + 1, subs.Length - insertIndex);
                root.subSystemList = newArr;
                return true;
            }
        }

        if (scanChildren == false) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (InsertAfter(ref subs[i], existing, custom, true, offset))
            {
                root.subSystemList = subs;
                return true;
            }
        }

        return false;
    }

    // ----- children insertion helpers -------------------------------------

    static bool InsertAsFirstChild(
        ref PlayerLoopSystem root,
        Type parent,
        in PlayerLoopSystem custom,
        bool scanChildren,
        int offset)
    {
        var subs = root.subSystemList;
        if (subs == null) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i].type == parent)
            {
                var childSubs = subs[i].subSystemList;
                int insertIndex = 0;

                if (childSubs != null)
                {
                    for (int j = 0; j < childSubs.Length; j++)
                    {
                        if (_registeredCustomTypes.Contains(childSubs[j].type) &&
                            _customSystemOffsets.TryGetValue(childSubs[j].type, out int existingOffset) &&
                            existingOffset <= offset)
                        {
                            insertIndex = j + 1;
                        }
                        else break;
                    }
                }

                int len = childSubs?.Length ?? 0;
                var newArr = new PlayerLoopSystem[len + 1];

                if (len > 0 && insertIndex > 0)
                    Array.Copy(childSubs, 0, newArr, 0, insertIndex);

                newArr[insertIndex] = custom;

                if (len > 0)
                    Array.Copy(childSubs, insertIndex, newArr, insertIndex + 1, len - insertIndex);

                subs[i].subSystemList = newArr;
                root.subSystemList = subs;
                return true;
            }
        }

        if (scanChildren == false) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (InsertAsFirstChild(ref subs[i], parent, custom, true, offset) == false) continue;
            root.subSystemList = subs;
            return true;
        }

        return false;
    }

    static bool InsertAsLastChild(
        ref PlayerLoopSystem root,
        Type parent,
        in PlayerLoopSystem custom,
        bool scanChildren,
        int offset)
    {
        var subs = root.subSystemList;
        if (subs == null) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i].type == parent)
            {
                var childSubs = subs[i].subSystemList;
                int insertIndex = childSubs?.Length ?? 0;

                if (childSubs != null)
                {
                    for (int j = childSubs.Length - 1; j >= 0; j--)
                    {
                        if (_registeredCustomTypes.Contains(childSubs[j].type) &&
                            _customSystemOffsets.TryGetValue(childSubs[j].type, out int existingOffset) &&
                            existingOffset >= offset)
                        {
                            insertIndex = j;
                        }
                        else break;
                    }
                }

                int len = childSubs?.Length ?? 0;
                var newArr = new PlayerLoopSystem[len + 1];

                if (len > 0 && insertIndex > 0)
                    Array.Copy(childSubs, 0, newArr, 0, insertIndex);

                newArr[insertIndex] = custom;

                if (len > 0)
                    Array.Copy(childSubs, insertIndex, newArr, insertIndex + 1, len - insertIndex);

                subs[i].subSystemList = newArr;
                root.subSystemList = subs;
                return true;
            }
        }

        if (scanChildren == false) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (InsertAsLastChild(ref subs[i], parent, custom, true, offset) == false) continue;
            root.subSystemList = subs;
            return true;
        }

        return false;
    }

    // ----- removal helper ---------------------------------------------------

    static bool RemoveInternal(
        ref PlayerLoopSystem root,
        Type target,
        bool scanChildren)
    {
        var subs = root.subSystemList;
        if (subs == null) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i].type == target)
            {
                var newArr = new PlayerLoopSystem[subs.Length - 1];
                Array.Copy(subs, 0, newArr, 0, i);
                Array.Copy(subs, i + 1, newArr, i, subs.Length - i - 1);
                root.subSystemList = newArr;
                return true;
            }
        }

        if (scanChildren == false) return false;

        for (int i = 0; i < subs.Length; i++)
        {
            if (RemoveInternal(ref subs[i], target, true))
            {
                root.subSystemList = subs;
                return true;
            }
        }

        return false;
    }
}
#endif