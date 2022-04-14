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
                                listBuffer[_stackOffset].Pop(); //now it can be popped, we continue the iteration
                            else
                            {
                                //in order to be able to reuse the task collection, we will keep the stack 
                                //in its original state (the original stack is not popped). 
                                _stackOffset++; //we move to the next task
                                goto breakInnerLoop;
                            }
                            break;
                        case TaskState.breakIt:
                            return true; //iteration done
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

        int _stackOffset;
    }
}