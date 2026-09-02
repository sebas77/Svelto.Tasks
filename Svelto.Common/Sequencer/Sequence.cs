using System;
using System.Collections.Generic;
using System.Reflection;
using DBC.Common;
using Svelto.DataStructures;

namespace Svelto.Common
{
    public interface ISequenceOrder
    {
        string[] enginesOrder { get; }
    }

    static class SequenceCache<SequenceOrder> where SequenceOrder : struct, ISequenceOrder
    {
        internal static Dictionary<string, int> cachedEnum;

        static SequenceCache()
        {
            cachedEnum = new Dictionary<string, int>();

            var values  = new SequenceOrder().enginesOrder;
            var counter = 0;

            Check.Require(values != null
                        , $"The sequence array {typeof(SequenceOrder).Name} hasn't been properly setup");

            foreach (var name in values)
                try
                {
                    cachedEnum.Add(name, counter++);
                }
                catch
                {
                    throw new Exception($"Order Sequence {typeof(SequenceOrder).Name} has duplicated entry {name}");
                }
        }
    }

    /// <summary>
    /// The sequencer relies on the attribute SequenceAttribute to allow classes to stay internal in their own
    /// assemblies.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="En"></typeparam>
    public class Sequence<T, En> where En : struct, ISequenceOrder
    {
        public Sequence(FasterReadOnlyList<T> itemsToSort)
        {
            _ordered = FasterList<T>.PreInit((uint) SequenceCache<En>.cachedEnum.Count);
            var counted    = 0;
            var cachedEnum = SequenceCache<En>.cachedEnum;
            foreach (var item in itemsToSort)
            {
                var type = item.GetType();

                Check.Require(type.IsDefined(typeof(SequencedAttribute))
                            , $"Sequenced item not tagged as Sequenced {type.Name}");
                var typeName = type.GetCustomAttribute<SequencedAttribute>().name;
                Check.Require(cachedEnum.ContainsKey(typeName)
                            , $"Sequenced item not contained in the sequence {typeof(En).Name}: {typeName}");
                var index = cachedEnum[typeName];
                counted++;
                Check.Require(_ordered[index] == null
                            , $"Items to sequence contains duplicate, {typeName} (wrong sequenced attribute name?)");
                _ordered[index] = item;
            }

#if DEBUG && !PROFILE_SVELTO
            if (counted != cachedEnum.Count)
            {
                var debug = new HashSet<string>();

                foreach (var debugItem in itemsToSort)
                    debug.Add(debugItem.GetType().GetCustomAttribute<SequencedAttribute>().name);

                foreach (var debugSequence in cachedEnum.Keys)
                    if (debug.Contains(debugSequence) == false)
                        throw new Exception(
                            $"The sequence {typeof(En).Name} wasn't fully satisfied, missing sequenced item {debugSequence}");
            }
#endif
        }

        public FasterReadOnlyList<T> items => new FasterReadOnlyList<T>(_ordered);

        public void Remove(int index)
        {
            if (index >= 0 && index < _ordered.count)
                _ordered[index] = default;
        }

        readonly FasterList<T> _ordered;
    }

    public class SequencedAttribute : Attribute
    {
        public SequencedAttribute(string name) { this.name = name; }

        internal string name { get; }
    }
}
