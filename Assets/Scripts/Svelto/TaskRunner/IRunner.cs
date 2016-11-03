
namespace Svelto.Tasks
{
    public interface IRunner
    {
        bool    paused { get; set; }
        bool    stopped { get; }

        void	StartCoroutine(PausableTask task);
        void    StartCoroutineThreadSafe(PausableTask task);
        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
    }
}
