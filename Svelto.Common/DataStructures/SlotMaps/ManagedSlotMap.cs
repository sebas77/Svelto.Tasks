namespace Svelto.DataStructures
{
    ///
    /// An experimental approach to sparse sets
    /// 
    /// A set is just about knowing if an entity identified through an id exists or doesn't exist in the set
    /// that's why bitsets can be used, it's just about true or false, there is or there isn't in the set.
    /// in ECS we can have a set for each component and list what entities are in that set
    ///
    /// entities: 0, 1, 2, 3, 4, 5, 6
    ///
    /// component1: 1, 5, 6
    /// component2: 2, 3, 5
    ///
    /// it's possible to intersect bitsets to know the entities that have all the components shared
    ///
    /// it is confirmed that when using a bitset this operation is necessary components[denseset[i]]
    ///
    /// The following class is not a sparse set, it's a more optimised dictionary for cases where the user
    /// cannot decide the key value.
    /// 
    public class ManagedSlotMap<T>
    {
        SlotMap<T, ManagedStrategy<T>, ManagedStrategy<SparseIndex>> _container;

        public ManagedSlotMap(uint initialSize)
        {
            _container = new SlotMap<T, ManagedStrategy<T>, ManagedStrategy<SparseIndex>>(initialSize);
        }

        public int capacity => _container.capacity;

        public int count => _container.count;

        public T this[ValueIndex index] => _container[index];

        public void Clear()
        {
            _container.Clear();
        }

        public bool Has(ValueIndex index)
        {
            return _container.Has(index);
        }

        public ValueIndex Add(T val)
        {
            return _container.Add(val);
        }

        public void Remove(ValueIndex index)
        {
            _container.Remove(index);
        }

        public void Reserve(uint u)
        {
            _container.Reserve(u);
        }

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}