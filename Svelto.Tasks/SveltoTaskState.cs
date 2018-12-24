using Svelto.Utilities;

namespace Svelto.Tasks
{
        internal struct SveltoTaskState
        {
            byte _value;

            const byte COMPLETED_BIT            = 0x1;
            const byte STARTED_BIT              = 0x2;
            const byte EXPLICITLY_STOPPED       = 0x4;
            const byte TASK_ENUMERATOR_JUST_SET = 0x8;
            const byte PAUSED_BIT               = 0x10;
            const byte PENDING_BIT              = 0x20;

            public bool completed
            {
                get { return BIT(COMPLETED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(COMPLETED_BIT);
                    else 
                        UNSETBIT(COMPLETED_BIT);
                }
            }
            
            public bool pendingTask
            {
                get { return BIT(PENDING_BIT); }
                set
                {
                    if (value) 
                        SETBIT(PENDING_BIT);
                    else 
                        UNSETBIT(PENDING_BIT);
                }
            }

            public bool explicitlyStopped
            {
                get { return BIT(EXPLICITLY_STOPPED); }
                set
                {
                    if (value) 
                        SETBIT(EXPLICITLY_STOPPED);
                    else 
                        UNSETBIT(EXPLICITLY_STOPPED);
                }
            }

            public bool paused
            {
                get { return BIT(PAUSED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(PAUSED_BIT);
                    else 
                        UNSETBIT(PAUSED_BIT);
                }
            }

            public bool started
            {
                get { return BIT(STARTED_BIT); }
                set
                {
                    if (value) 
                        SETBIT(STARTED_BIT);
                    else 
                        UNSETBIT(STARTED_BIT);
                }
            }

            public bool taskEnumeratorJustSet
            {
                get { return BIT(TASK_ENUMERATOR_JUST_SET); }
                set
                {
                    if (value) 
                        SETBIT(TASK_ENUMERATOR_JUST_SET);
                    else 
                        UNSETBIT(TASK_ENUMERATOR_JUST_SET);
                }
            }

            void SETBIT(byte bitmask)
            {
                ThreadUtility.VolatileWrite(ref _value, (byte) (_value | bitmask));
            }

            void UNSETBIT(int bitmask)
            {
                ThreadUtility.VolatileWrite(ref _value, (byte) (_value & ~bitmask));
            }

            bool BIT(byte bitmask)
            {
                return (ThreadUtility.VolatileRead(ref _value) & bitmask) == bitmask;
            }

            public bool isRunning
            {
                get
                {
                    byte completedAndStarted = STARTED_BIT | COMPLETED_BIT;

                    //started but not completed
                    return (ThreadUtility.VolatileRead(ref _value) & completedAndStarted) == STARTED_BIT;
                }
            }

            public bool isDone
            {
                get
                {
                    byte completedAndStarted = COMPLETED_BIT | STARTED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndStarted) == COMPLETED_BIT;
                }
            }

            public bool isNotCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndPaused) == 0x0;
                }
            }
            
            public bool isCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (ThreadUtility.VolatileRead(ref _value) & completedAndPaused) == COMPLETED_BIT;
                }
            }
        }
 }