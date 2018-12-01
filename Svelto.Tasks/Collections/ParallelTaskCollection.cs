using System;
using System.Collections;
using UnityEditor;

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
            int tasksYielded = 0;
            
            while (taskCount - _stackOffset - tasksYielded > 0)
            {
                var listBuffer = rawListOfStacks;
                for (int index = 0; index < taskCount - _stackOffset; ++index)
                {
                    if (listBuffer[index].count > 0)
                    {
                        var processStackAndCheckIfDone = ProcessStackAndCheckIfDone(index);
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
                                    index = SwapStack(index, listBuffer, taskCount, tasksYielded);
                                    _stackOffset++;
                                }

                                break;
                            case TaskState.breakIt:
                                return true;
                            case TaskState.continueIt:
                                break;
                            case TaskState.yieldIt:
                            {
                                tasksYielded++; //todo this must be thoroughly unit tested 
                                break;
                            }
                        }
                    }
                }
            }

            if (tasksYielded > 0)
                return false;

            _stackOffset = 0;

            return true;
        }

        protected override void ProcessTask(ref T Task)
        {}

        int SwapStack(int index, StructFriendlyStack[] buffer, int count, int numberOfTasksYielded)
        {
            if (numberOfTasksYielded == 0)
            {
                var lastIndex = count - _stackOffset - 1;

                if (index == lastIndex) //is this the last index available, then don't swap 
                    return index;

                var item = buffer[lastIndex];
                buffer[lastIndex] = buffer[index];
                buffer[index]     = item;
                
                return --index;
            }
            else
            {
                var lastIndex = count - _stackOffset -  1;
                
                var item = buffer[lastIndex]; //remember the last yielded object
                buffer[lastIndex] = buffer[index]; //replace the last yielded object with the item just completed
                var item2 = buffer[lastIndex - numberOfTasksYielded]; //remember the item before the first yielded one
                buffer[lastIndex - numberOfTasksYielded] = item; //replace the item before the 1st yielded one with the last yielded
                buffer[index] = item2; //replace the index of the ended object with the one from before the 1st yielded one
                
                return --index;
            }
        }

        int _stackOffset;
    }
}