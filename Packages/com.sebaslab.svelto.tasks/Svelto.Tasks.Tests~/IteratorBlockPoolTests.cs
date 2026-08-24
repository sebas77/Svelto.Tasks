using System.Collections.Generic;
using Svelto.Tasks.Lean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class IteratorBlockPoolTests
    {
        class PoolData
        {
            public int value;
        }

        [Test]
        public void Lean_IteratorBlockPool_RecyclesBlocks()
        {
            // What we are testing:
            // IteratorBlockPool should reuse PooledIteratorBlock instances after they are released.

            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                //this is a special pattern that allows us to test the pool's recycling behavior.
                //The iterator will yield indefinitely, allowing us to control when it completes
                //and release it back to the pool without never actually finishing the logic inside the iterator.
                while (true)
                {
                    data.value++;
                    yield return TaskContract.Break.It;
                }
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "TestPool");

            // Get first block
            (PoolData data1, PooledIteratorBlock<PoolData> block1) = pool.Get();
            data1.value = 0; //the idea is that Data must always be initialised before to start to be used
            
            // Run it to completion
            block1.MoveNext(); // first step
            Assert.That(data1.value, Is.EqualTo(1));
            
            block1.MoveNext(); // second step (completes and releases)
            Assert.That(data1.value, Is.EqualTo(2));
            
            // block1 should have been released to pool now
            
            // Get second block
            var (data2, block2) = pool.Get();
            
            Assert.That(data2, Is.SameAs(data1));
            Assert.That(block2, Is.SameAs(block1));
            
            // Verify it actually runs again (not stuck in finished state)
            data2.value = 0;
            block2.MoveNext();
            Assert.That(data2.value, Is.EqualTo(1));
        }
        
        [Test]
        public void ExtraLean_IteratorBlockPool_RecyclesBlocks()
        {
            // What we are testing:
            // ExtraLean IteratorBlockPool should also recycle blocks.
            
            System.Collections.IEnumerator MyIterator(PoolData data)
            {
                while (true)
                {
                    data.value++;
                    yield return TaskContract.Break.It;
                }
            }

            var pool = new Svelto.Tasks.ExtraLean.IteratorBlockPool<PoolData>(MyIterator, "ExtraLeanTestPool");

            var (data1, block1) = pool.Get();
            data1.value = 0; //the idea is that Data must always be initialised before to start to be used
            
            // Run it to completion
            block1.MoveNext(); // first step
            Assert.That(data1.value, Is.EqualTo(1));
            
            block1.MoveNext(); // second step (completes and releases)
            Assert.That(data1.value, Is.EqualTo(2));
            
            // block1 should have been released to pool now
            
            // Get second block
            var (data2, block2) = pool.Get();
            
            Assert.That(data2, Is.SameAs(data1));
            Assert.That(block2, Is.SameAs(block1));
            
            // Verify it actually runs again (not stuck in finished state)
            data2.value = 0;
            block2.MoveNext();
            Assert.That(data2.value, Is.EqualTo(1));
        }
    }
}

