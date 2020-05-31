namespace Svelto.Tasks
{
    public class Break
    {
        /// <summary>
        /// A Break.It task breaks but to not break the caller task. Break.It is meant to be used by tasks
        /// called by other tasks. A task with break.it can be cached if it runs through a while (true) loop.
        /// the task is completed and removed from the queue on each Break.It but the enumerator can be reused from
        /// the calling task next frame as it's not completed for the CLR
        /// </summary>
        public static Break It = new Break();
        /// <summary>
        /// Break.AndStop breaks the task and the calling tasks too
        /// </summary>
        public static Break AndStop = new Break();
    }
}
