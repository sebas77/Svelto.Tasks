using System.Collections;

namespace Svelto.Tasks
{
    public class ParallelTaskCollection : ParallelTaskCollection<IEnumerator>
    {
        public ParallelTaskCollection()
        {}
        
        public ParallelTaskCollection(int initialSize) : base(initialSize)
        {}
        
        public ParallelTaskCollection(IEnumerator[] ptasks) : base(ptasks)
        {}
    }
    
    public class ParallelTaskCollection<T>: TaskCollection<T> where T:IEnumerator
    {
        const int _INITIAL_STACK_COUNT = 3;
        
        public ParallelTaskCollection():base(_INITIAL_STACK_COUNT)
        {}
        
        public ParallelTaskCollection(int initialSize) : base(initialSize)
        {}

        public ParallelTaskCollection(T[] ptasks):base(_INITIAL_STACK_COUNT)
        {
            for (int i = 0; i < ptasks.Length; i++)
                Add(ptasks[i]);
        }

        protected override bool RunTasksAndCheckIfDone()
        {
            while (taskCount - _stackOffset > 0)
            {
                var listBuffer = rawListOfStacks;
                for (int index = 0; index < taskCount - _stackOffset; ++index)
                {
                    if (listBuffer[index].count > 0)
                    {
                        var processStackAndCheckIfDone = ProcessStackAndCheckIfDone();
                        switch (processStackAndCheckIfDone)
                            {
                                case TaskState.doneIt:
                                    if (listBuffer[index].count > 1)
                                        listBuffer[index].Pop(); //now it can be popped
                                    else
                                    {
                                        //in order to be able to reuse the task collection, we will keep the stack 
                                        //in its original state. The tasks will be shuffled, but due to the nature
                                        //of the parallel execution, it doesn't matter.
                                        index = RemoveStack(index, listBuffer, taskCount); 
                                    }
                                    break;
                                case TaskState.breakIt:
                                    return true;
                                case TaskState.continueIt:
                                    break;
                                case TaskState.yieldIt:
                                    break;
                            }
                    }
                }

                return false;
            }
            return true;
        }

        protected override void ProcessTask(ref T Task)
        {}

        int RemoveStack(int index, StructFriendlyStack[] buffer, int count)
        {
            var lastIndex = count - _stackOffset - 1;

            _stackOffset++;

            if (index == lastIndex)
                return index;

            var item = buffer[lastIndex];
            buffer[lastIndex] = buffer[index];
            buffer[index]     = item;

            return --index;
        }
    }
}