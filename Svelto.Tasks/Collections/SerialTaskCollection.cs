using System.Collections.Generic;

namespace Svelto.Tasks
{
    public class SerialTaskCollection : SerialTaskCollection<IEnumerator<TaskContract>>
    {
        public SerialTaskCollection() {}
        
        public SerialTaskCollection(int initialSize) : base(initialSize) {}
        
        public SerialTaskCollection(string name): base(name) {}

        public SerialTaskCollection(string name, int initialSize) : base(name, initialSize) {}
    }

    
    /// <summary>
    /// TaskCollections are still not tested with the new logic. Returning a .Complete may not work, must be
    /// unit tested properly
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SerialTaskCollection<T> : TaskCollection<T> where T : IEnumerator<TaskContract>
    {
        const int _INITIAL_STACK_COUNT = 1;

        public SerialTaskCollection() : base(_INITIAL_STACK_COUNT) {}
        
        public SerialTaskCollection(int initialSize) : base(initialSize) {}

        public SerialTaskCollection(string name) : base(name, _INITIAL_STACK_COUNT) {}

        public SerialTaskCollection(string name, int initialSize) : base(name, initialSize) {}

        protected override bool RunTasksAndCheckIfDone()
        {
            while (_stackOffset < taskCount)
            {
                var listBuffer = rawListOfStacks;
                while (listBuffer[_stackOffset].count > 0)
                {
                    var processStackAndCheckIfDone = ProcessStackAndCheckIfDone(_stackOffset);
                    switch (processStackAndCheckIfDone)
                    {
                        case TaskState.doneIt:
                            if (listBuffer[_stackOffset].count > 1) //there is still something to do with this task
                                listBuffer[_stackOffset].Pop(); //Pop() disposes the completed child, the parent resumes
                            else
                            {
                                //“Which root-task slot should I resume from on the next MoveNext()?”
                                //in order to be able to reuse the task collection, we will keep the stack 
                                //in its original state (the original stack is not popped). 
                                _stackOffset++; //we move to the next task
                                goto breakInnerLoop;
                            }
                            break;
                        case TaskState.breakIt:
                            //Break.AndStop: cancel everything and let MoveNext yield StopParentChain
                            StopChain();
                            return true;
                        case TaskState.continueIt: 
                            continue; //continue with the current task 
                        case TaskState.yieldIt:
                            return false; //continue the iteration next frame
                    }
                }

                breakInnerLoop: ; //move to the next task
            }

            _stackOffset = 0;

            return true;
        }
        
        public override void Reset()
        {
            base.Reset();

            _stackOffset = 0;
        }
        
        public new void Clear()
        {
            base.Clear();

            _stackOffset = 0;
        }

        int _stackOffset;
    }

    /// <summary>
    /// DO NOT THINK TO ADD static pools, because they could be improperly used and leak all over the place
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class StackTask<T> : SerialTaskCollection where T : IEnumerator<TaskContract>
    {
        const int _INITIAL_STACK_COUNT = 1;

        public StackTask() : base(_INITIAL_STACK_COUNT) { }

        public StackTask(string name) : base(name, _INITIAL_STACK_COUNT) { }

        public void Reset(IEnumerator<TaskContract> loadAndCachePrefab)
        {
            base.Reset();
            
            Add(loadAndCachePrefab);
        }
    }
}