using System;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Svelto.Common;
using Svelto.DataStructures;

namespace Svelto.Common.Tests
{
    [TestFixture]
    public partial class NativeBagTests
    {
        [StructLayout(LayoutKind.Explicit, Size = 7)]
        struct weidStruct
        {
            [FieldOffset(0)] public byte  a; 
            [FieldOffset(3)] public uint  b;
            [FieldOffset(1)] public short c;
        }
        
        [StructLayout(LayoutKind.Sequential, Pack=1, Size = 7)]
        struct weidStruct2
        {
            public byte  a; 
            public uint  b;
            public short c;
        }
        
        struct normal
        {
            public byte  a; 
            public uint  b;
            public short c;
        }
        
        [Test]
        public void TestWeirdStract()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue(new weidStruct
                    {
                        a = 13, b = 1023, c = 2356
                    });
                    _simpleNativeBag.Enqueue(new weidStruct2
                    {
                        a = 13, b = 1023, c = 2356
                    });
                    _simpleNativeBag.Enqueue(new normal
                    {
                        a = 13, b = 1023, c = 2356
                    });
                    _simpleNativeBag.Enqueue(i);
                }
                
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 16; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }
                
                for (var i = 0; i < 16; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<weidStruct>(), Is.EqualTo(new weidStruct
                    {
                        a = 13, b = 1023, c = 2356
                    }));
                    Assert.That(_simpleNativeBag.Dequeue<weidStruct2>(), Is.EqualTo(new weidStruct2
                    {
                        a = 13, b = 1023, c = 2356
                    }));
                    Assert.That(_simpleNativeBag.Dequeue<normal>(), Is.EqualTo(new normal
                    {
                        a = 13, b = 1023, c = 2356
                    }));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(i));
                }
                
                for (var i = 0; i < 16; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }
            }
        }

        [Test]
        public void TestByteReallocWorks()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                    _simpleNativeBag.Enqueue((byte) 0);

                Assert.That(_simpleNativeBag.count, Is.EqualTo(128));
            }
        }

        [Test]
        public void TestCantDequeMoreThanQueue()
        {
#if DEBUG
            Assert.Throws<Exception>(() =>
            {
                using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 0);

                    for (var i = 0; i < 3; i++)
                    {
                        _simpleNativeBag.Enqueue((byte) 0);
                        _simpleNativeBag.Dequeue<byte>();
                        _simpleNativeBag.Dequeue<byte>();
                    }

                    Assert.That(_simpleNativeBag.count, Is.EqualTo(32));
                }
            });
#else
            Assert.Pass("NativeBag bounds checks are intentionally compiled out of Release builds");
#endif
        }

        [Test]
        public void TestDoofusesScenario()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((uint) i);
                    _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                }

                var index = 0;

                while (_simpleNativeBag.IsEmpty() == false)
                {
                    Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(index));
                    var dequeue = _simpleNativeBag.Dequeue<EGID>();
                    index++;
                    Assert.That(_simpleNativeBag.count == 32 * 12 - index * 12);
                    Assert.That(dequeue.entityID, Is.EqualTo(1));
                    Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                }
            }
        }
        
        [Test]
        public void TestDoofusesScenarioByte()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) i);
                    _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                }

                var index = 0;

                while (_simpleNativeBag.IsEmpty() == false)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(index));
                    var dequeue = _simpleNativeBag.Dequeue<EGID>();
                    index++;
                    Assert.That(_simpleNativeBag.count == 32 * 12 - index * 12);
                    Assert.That(dequeue.entityID, Is.EqualTo(1));
                    Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void TestDoofusesScenario2()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                var dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
            }
        }

        [Test]
        public void TestDoofusesScenario3()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                var dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct()));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(0));
            }
        }

        struct ExclusiveGroupStruct
        {
            internal uint group;
            public ExclusiveGroupStruct(uint i) { @group = i; }
        }

        struct EGID
        {
            internal uint entityID;
            internal uint groupID;

            public EGID(uint i, ExclusiveGroupStruct exclusiveGroupStruct)
            {
                entityID = i;
                @groupID = exclusiveGroupStruct.group;
            }
        }

        [Test]
        public void TestDoofusesScenario4()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                var dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct EGIDU
        {
            internal uint         entityID;
            ExclusiveGroupStructU exclusiveGroupStruct;
            internal short        groupID => exclusiveGroupStruct.group;

            public EGIDU(uint i, ExclusiveGroupStructU exclusiveGroupStruct)
            {
                entityID                  = i;
                this.exclusiveGroupStruct = exclusiveGroupStruct;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct ExclusiveGroupStructU
        {
            internal short group;
            byte           test;
            public ExclusiveGroupStructU(uint i) : this() { @group = (short) i; }
        }

        [Test]
        public void TestDoofusesScenario4Unaligned()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                var dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(1)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGIDU(1, new ExclusiveGroupStructU(3)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGIDU>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));
            }
        }

        [Test]
        public void TestDoofusesScenario5()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(1)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                var dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(1));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(2)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(3)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(4)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(5)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(2));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(3));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(4));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(5));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(6)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(7)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(8)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(9)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(6));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(7));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(8));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(9));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(10)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(10));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(11)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(12)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(11));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(12));

                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(13)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(14)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(15)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(16)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(17)));
                _simpleNativeBag.Enqueue((uint) 1);
                _simpleNativeBag.Enqueue(new EGID(1, new ExclusiveGroupStruct(18)));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(13));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(14));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(15));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(16));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(17));

                Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(1));
                dequeue = _simpleNativeBag.Dequeue<EGID>();
                Assert.That(dequeue.entityID, Is.EqualTo(1));
                Assert.That((uint) dequeue.groupID, Is.EqualTo(18));
            }
        }

        [Test]
        public void TestEnqueueDequeueWontResize()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue<byte>(0);
                    _simpleNativeBag.Dequeue<byte>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(4));
            }
        }

        [Test]
        public void TestEnqueueDequeueWontAllocTooMuch()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(28));
            }
        }

        [Test]
        public void TestEnqueueDequeueWontAllocTooMuchOnce()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(4));
                    _simpleNativeBag.Enqueue((byte) 0);
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(12));
                    _simpleNativeBag.Enqueue((byte) 0);
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(12));
                    _simpleNativeBag.Enqueue((byte) 0);
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(28));
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(28));
            }
        }

        struct Weird
        {
            public short a;
            public byte  b;
        }

        [Test]
        public void TestEnqueueDequeueWontAllocTooMuchWithWeirdStruct()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue(new Weird()); //8
                    _simpleNativeBag.Enqueue(new Weird()); //OK
                    _simpleNativeBag.Enqueue(new Weird()); //24
                    _simpleNativeBag.Enqueue(new Weird()); //ok
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(24));
            }
        }

        [Test]
        public void TestEnqueueDequeueWontAllocTooMuchWithWeirdStructOnce()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                {
                    _simpleNativeBag.Enqueue(new Weird()); //8
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(8));
                    _simpleNativeBag.Enqueue(new Weird()); //OK
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(8));
                    _simpleNativeBag.Enqueue(new Weird()); //24
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(24));
                    _simpleNativeBag.Enqueue(new Weird()); //ok
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                    _simpleNativeBag.Dequeue<Weird>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(24));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Weird2
        {
            public short a;
            public byte  b;
        }

        [Test]
        public void TestEnqueueDequeueWontAllocTooMuchWithWeirdStructUnalignedOnce()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                {
                    _simpleNativeBag.Enqueue(new Weird2()); //8
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(8));
                    _simpleNativeBag.Enqueue(new Weird2()); //OK
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(8));
                    _simpleNativeBag.Enqueue(new Weird2()); //24
                    Assert.That(_simpleNativeBag.capacity, Is.EqualTo(24));
                    _simpleNativeBag.Enqueue(new Weird2()); //ok
                    _simpleNativeBag.Dequeue<Weird2>();
                    _simpleNativeBag.Dequeue<Weird2>();
                    _simpleNativeBag.Dequeue<Weird2>();
                    _simpleNativeBag.Dequeue<Weird2>();
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(24));
            }
        }

        [Test]
        public void TestEnqueueTwiceDequeueOnceLeavesWithHalfOfTheEntities()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Dequeue<byte>();
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(128));
            }
        }

        [Test]
        public void TestLongReallocWorks()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                    _simpleNativeBag.Enqueue((long) 0);

                Assert.That(_simpleNativeBag.count, Is.EqualTo(32 * 8));
            }
        }

        [Test]
        public void TestMixedReallocWorks()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 2; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((uint) 0);
                    _simpleNativeBag.Enqueue((long) 0);
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(32));
            }
        }

        [Test]
        public void TestUintReallocWorks()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                    _simpleNativeBag.Enqueue((uint) 0);

                Assert.That(_simpleNativeBag.count, Is.EqualTo(32 * 4));
            }
        }

        [Test]
        public void TestWhatYouEnqueueIsWhatIDequeue()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(28));
            }
        }

        [Test]
        public void TestWhatYouEnqueueIsWhatIDequeueMixed()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0); //4
                    _simpleNativeBag.Enqueue(new Weird2()
                    {
                        a = 0xFA
                      , b = 7
                    }); //(4 + 4) * 2 = 16
                    _simpleNativeBag.Enqueue(new Weird()
                    {
                        a = 0xFA
                      , b = 7
                    });
                    _simpleNativeBag.Enqueue(new Weird2()
                    {
                        a = 0xFA
                      , b = 7
                    });
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<Weird2>(), Is.EqualTo(new Weird2()
                    {
                        a = 0xFA
                      , b = 7
                    }));
                    Assert.That(_simpleNativeBag.Dequeue<Weird>(), Is.EqualTo(new Weird()
                    {
                        a = 0xFA
                      , b = 7
                    }));
                    Assert.That(_simpleNativeBag.Dequeue<Weird2>(), Is.EqualTo(new Weird2()
                    {
                        a = 0xFA
                      , b = 7
                    }));
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(16));
            }
        }
        
        
        [Test]
        public void TestWriteIndexIsDecoupledFromWrapping()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 8; i++)
                {
                    _simpleNativeBag.Dequeue<byte>();
                }
                
                var currentCapacity = _simpleNativeBag.capacity;
                
                _simpleNativeBag.Enqueue<byte>(0);
                _simpleNativeBag.Enqueue<byte>(1);
                _simpleNativeBag.Enqueue<byte>(2);
                _simpleNativeBag.Enqueue<byte>(3);
                _simpleNativeBag.Enqueue<byte>(4);
                _simpleNativeBag.Enqueue<byte>(5);
                _simpleNativeBag.Enqueue<byte>(6);
                _simpleNativeBag.Enqueue<byte>(7);

                Assert.That(_simpleNativeBag.capacity == currentCapacity);
                currentCapacity = _simpleNativeBag.capacity;
                
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                }
                
                Assert.That(_simpleNativeBag.capacity > currentCapacity);
                
                for (var i = 0; i < 30; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }
                
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(4));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(5));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(6));
                Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(7));
                
                for (var i = 0; i < 32; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(255));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(255));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(255));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(255));
                }
            }
        }

        [Test]
        public void TestReallocReserved()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 8; i++)
                {
                    _simpleNativeBag.Dequeue<byte>();
                }
                
                var currentCapacity = _simpleNativeBag.capacity;
                
                _simpleNativeBag.ReserveEnqueue<byte>(out var index0) = (byte) 0;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index1) = (byte) 1;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index2) = (byte) 2;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index3) = (byte) 3;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index4) = (byte) 4;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index5) = (byte) 5;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index6) = (byte) 6;
                _simpleNativeBag.ReserveEnqueue<byte>(out var index7) = (byte) 7;

                Assert.That(_simpleNativeBag.capacity == currentCapacity);
                currentCapacity = _simpleNativeBag.capacity;
                
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                }
                
                Assert.That(_simpleNativeBag.capacity > currentCapacity);
                
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index0), Is.EqualTo((byte) 0));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index1), Is.EqualTo((byte) 1));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index2), Is.EqualTo((byte) 2));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index3), Is.EqualTo((byte) 3));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index4), Is.EqualTo((byte) 4));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index5), Is.EqualTo((byte) 5));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index6), Is.EqualTo((byte) 6));
                Assert.That(_simpleNativeBag.AccessReserved<byte>(index7), Is.EqualTo((byte) 7));
            }
        }
        
        [Test]
        public void TestWrappedAndReallocReserved()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 8; i++)
                {
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                }
                
                var currentCapacity = _simpleNativeBag.capacity;

                var indices = new UnsafeArrayIndex[32 * 4]; 
                
                for (var i = 0; i < 16 * 4; i++)
                {
                    _simpleNativeBag.ReserveEnqueue<byte>(out indices[i]) = (byte) i;
                }
                
                Assert.That(_simpleNativeBag.capacity == currentCapacity); //we want to wrap, not realloc
                
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                    _simpleNativeBag.Enqueue((byte) 255);
                }
                
                Assert.That(_simpleNativeBag.capacity > currentCapacity); //we want to wrap, not realloc
                
                for (var i = 0; i < 16 * 4; i++)
                {
                    Assert.That(_simpleNativeBag.AccessReserved<byte>(indices[i]), Is.EqualTo((byte) i));
                }
            }
        }
        
        [Test]
        public void TestWrappedReserved()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }
                
                for (var i = 0; i < 8; i++)
                {
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                    _simpleNativeBag.Dequeue<byte>();
                }
                
                var currentCapacity = _simpleNativeBag.capacity;

                var indices = new UnsafeArrayIndex[32 * 4]; 
                
                for (var i = 0; i < 16 * 4; i++)
                {
                    _simpleNativeBag.ReserveEnqueue<byte>(out indices[i]) = (byte) i;
                }
                
                Assert.That(_simpleNativeBag.capacity == currentCapacity); //we want to wrap, not realloc
                
                for (var i = 0; i < 16 * 4; i++)
                {
                    Assert.That(_simpleNativeBag.AccessReserved<byte>(indices[i]), Is.EqualTo((byte) i));
                }
            }
        }

        [Test]
        public void TestWhatYouEnqueueIsWhatIDequeue2()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }

                for (var i = 0; i < 32; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }

                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }

                for (var i = 0; i < 32; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }

                for (var i = 0; i < 32; i++)
                {
                    _simpleNativeBag.Enqueue((byte) 0);
                    _simpleNativeBag.Enqueue((byte) 1);
                    _simpleNativeBag.Enqueue((byte) 2);
                    _simpleNativeBag.Enqueue((byte) 3);
                }

                for (var i = 0; i < 32; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(0));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(1));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(2));
                    Assert.That(_simpleNativeBag.Dequeue<byte>(), Is.EqualTo(3));
                }

                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(1020));
            }
        }

        [Test]
        public void TestReaderGreaterThanWriter()
        {
            using (var _simpleNativeBag = new NativeBag(Allocator.Persistent))
            {
                //write 16 uint. The writerHead will be at the end of the array
                for (var i = 0; i < 16; i++)
                {
                    _simpleNativeBag.Enqueue((uint) i);
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(64));
                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(120)); //

                //read 8 uint, the readerHead will be in the middle of the array
                for (var i = 0; i < 8; i++)
                {
                    Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(i));
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(32));
                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(120));

                //write 4 uint, now the writer head wrapped and it's before the reader head
                //capacity must stay unchanged
                for (var i = 16; i < 16 + 7; i++)
                {
                    _simpleNativeBag.Enqueue((uint) i);
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(60));
                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(120));

                //now I will surpass reader, so it won't change the capacity because there is enough space
                for (var i = 16 + 7; i < 16 + 7 + 2; i++)
                {
                    _simpleNativeBag.Enqueue((uint) i);
                }

                Assert.That(_simpleNativeBag.count, Is.EqualTo(68));
                Assert.That(_simpleNativeBag.capacity, Is.EqualTo(120));

                //dequeue everything and verify values
                int index = 8;
                while (_simpleNativeBag.IsEmpty())
                {
                    Assert.That(_simpleNativeBag.Dequeue<uint>(), Is.EqualTo(index));
                    index++;
                }
            }
        }
    }
}
