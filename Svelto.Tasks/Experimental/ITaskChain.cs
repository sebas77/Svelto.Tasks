namespace Svelto.Tasks.Experimental
{
    public interface ITaskChain<in Token> : IAbstractTask
    {
        void Execute(Token token);
    }
}
