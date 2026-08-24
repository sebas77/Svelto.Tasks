namespace Svelto.DataStructures
{
    public struct SveltoDictionaryNode<TKey>
    {
        internal int  hashcode;
        internal int  previous;
        public   TKey key;

        public SveltoDictionaryNode(in TKey key, int hash, int previousNode)
        {
            this.key = key;
            hashcode = hash;
            previous = previousNode;
        }

        public SveltoDictionaryNode(in TKey key, int hash)
        {
            this.key = key;
            hashcode = hash;
            previous = -1;
        }
    }
}