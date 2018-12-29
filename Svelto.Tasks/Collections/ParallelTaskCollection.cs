using System.Collections;

namespace Svelto.Tasks
{
    public class ParallelTaskCollection : ParallelTaskCollection<IEnumerator>
    {
        public ParallelTaskCollection() : base()
        
        {}
        public ParallelTaskCollection(string name, int initialSize) : base(name, initialSize)
        {}
        
        public ParallelTaskCollection(string name, IEnumerator[] ptasks) : base(name, ptasks)
        {}

        public ParallelTaskCollection(string name) : base(name)
        {}
    }
    
    public class ParallelTaskCollection<T>: TaskCollection<T> where T:IEnumerator
    {
        const int _INITIAL_STACK_COUNT = 3;
        
        public  ParallelTaskCollection() : base(_INITIAL_STACK_COUNT)
        {}
        
        public ParallelTaskCollection(string name):base(name, _INITIAL_STACK_COUNT)
        {}
        
        public ParallelTaskCollection(string name, int initialSize) : base(name, initialSize)
        {}

        public ParallelTaskCollection(string name, T[] ptasks):base(name, ptasks.Length)
        {
            for (int i = 0; i < ptasks.Length; i++)
                Add(ptasks[i]);
        }

        /// <summary>
        /// in a ParallelTasks scenario with N tasks, each task can ultimately have only two options: run
        /// synchronously on the runner, actually blocking the other tasks, or yielding. Only one yield per task
        /// per frame can happen. Therefore a ParalleTask or get stuck in one specific task until is done because
        /// it's running synchronously, or at a given point yield all the tasks on the current frame.
        /// Basically each tasks runs synchronously until the next MoveNext() that will yield the execution
        /// to the next task until there are no more tasks and therefore resuming the next iteration (or frame)
        /// </summary>
        /// <returns></returns>
        protected override bool RunTasksAndCheckIfDone()
        {
            var stacks = rawListOfStacks;
            for (int index = 0; index < taskCount - _stackOffset; ++index)
            {
                if (stacks[index].count > 0)
                {
                    var processStackAndCheckIfDone = ProcessStackAndCheckIfDone(index);
                    switch (processStackAndCheckIfDone)
                    {
                        case TaskState.doneIt:
                            if (stacks[index].count > 1)
                            {
                                stacks[index].Pop(); //now it can be popped
                                index--;             //continue the current task
                            }
                            else
                            {
                                //in order to be able to reuse the task collection, we will keep the stack 
                                //in its original state. The tasks will be shuffled, but due to the nature
                                //of the parallel execution, it doesn't matter.
                                index = SwapStack(index, stacks, taskCount);
                                _stackOffset++; 
                                //move to the next task
                            }
                            break;
                        case TaskState.breakIt:
                            return true;           //end the iteration
                        case TaskState.continueIt: //continue the current task
                            index--;
                            continue;
                        case TaskState.yieldIt:
                            continue; //continue with the next task
                    }
                }
            }

            if (taskCount - _stackOffset > 0)
                return false;

            _stackOffset = 0;

            return true;
        }

        protected override void ProcessTask(ref T Task)
        {}

        int SwapStack(int index, StructFriendlyStack[] buffer, int count)
        {
            var lastIndex = count - _stackOffset - 1;

            if (index == lastIndex) //is this the last index available, then don't swap 
                return index;

            var item = buffer[lastIndex];
            buffer[lastIndex] = buffer[index];
            buffer[index]     = item;
                
            return --index;
        }

        int _stackOffset;
    }
}