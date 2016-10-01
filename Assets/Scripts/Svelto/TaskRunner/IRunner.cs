using System.Collections;

namespace Svelto.Tasks
{
    public interface IRunner
    {
        bool    paused { get; set; }
        bool    stopped { get; }
        void	StartCoroutine(IEnumerator task);

        void 	StopAllCoroutines();

        int numberOfRunningTasks { get; }
    }
}
