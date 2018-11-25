namespace Svelto.Tasks.Experimental
{
    public interface IChainLink<in Token>
    {
        Token token { set; }
    }
}
