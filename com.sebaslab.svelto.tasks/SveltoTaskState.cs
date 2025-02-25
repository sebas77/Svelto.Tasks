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
                get => BIT(COMPLETED_BIT);
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
                get => BIT(PENDING_BIT);
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
                get => BIT(EXPLICITLY_STOPPED);
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
                get => BIT(PAUSED_BIT);
                set
                {
                    if (value) 
                        SETBIT(PAUSED_BIT);
                    else 
                        UNSETBIT(PAUSED_BIT);
                }
            }

            void SETBIT(byte bitmask)
            {
                System.Threading.Volatile.Write(ref _value, (byte) (_value | bitmask));
            }

            void UNSETBIT(int bitmask)
            {
                System.Threading.Volatile.Write(ref _value, (byte) (_value & ~bitmask));
            }

            bool BIT(byte bitmask)
            {
                return (System.Threading.Volatile.Read(ref _value) & bitmask) == bitmask;
            }

            public bool isRunning
            {
                get
                {
                    byte completedAndStarted = STARTED_BIT | COMPLETED_BIT;

                    //started but not completed
                    return (System.Threading.Volatile.Read(ref _value) & completedAndStarted) == STARTED_BIT;
                }
            }

            public bool isDone
            {
                get
                {
                    byte completedAndStarted = COMPLETED_BIT | STARTED_BIT;

                    return (System.Threading.Volatile.Read(ref _value) & completedAndStarted) == COMPLETED_BIT;
                }
            }

            public bool isNotCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (System.Threading.Volatile.Read(ref _value) & completedAndPaused) == 0x0;
                }
            }
            
            public bool isCompletedAndNotPaused
            {
                get
                {
                    byte completedAndPaused = COMPLETED_BIT | PAUSED_BIT;

                    return (System.Threading.Volatile.Read(ref _value) & completedAndPaused) == COMPLETED_BIT;
                }
            }
        }
 }