using System.Collections.Generic;
using System.Threading.Tasks;
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
            block1.MoveNext(); // first step (hits Break.It: the block is flagged for release)
            Assert.That(data1.value, Is.EqualTo(1));
            
            block1.MoveNext(); // second cycle (hits Break.It again, re-flags)
            Assert.That(data1.value, Is.EqualTo(2));
            
            block1.Dispose(); //a runner calls this automatically; a manual caller must: it returns the block to the pool
            
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

        [Test]
        public void Lean_IteratorBlockPool_AllowsConcurrentBorrowAndReturn()
        {
            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                while (true)
                    yield return TaskContract.Break.It;
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "ConcurrentLeanPool");

            Parallel.For(0, 10_000, _ =>
            {
                var (_, block) = pool.Get();
                block.MoveNext();  //hits Break.It: flags the block for release
                block.Dispose();   //what a runner does automatically: returns the block to the pool
            });

            Assert.That(pool.count, Is.GreaterThan(0));
            pool.Dispose();
        }

        [Test]
        public void Lean_IteratorBlockPool_RunnerCompletionRecyclesBlock()
        {
            // What we are testing:
            // The regression that motivated the Dispose-based ownership: a pooled block executed by a REAL
            // runner completes through Break.It, the runner's cleanup calls Dispose() and only then the block
            // is back in the pool, ready to be executed again by the same runner.

            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                while (true)
                {
                    data.value++;
                    yield return TaskContract.Yield.It;
                    yield return TaskContract.Break.It; //cycle boundary: the only reusable point
                }
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "RunnerRecyclePool");

            using (var runner = new SteppableRunner("RunnerRecyclePoolRunner"))
            {
                var (data1, block1) = pool.Get();
                data1.value = 0;

                block1.RunOn(runner);
                runner.WaitForTasksDone(16, 2000);

                Assert.That(data1.value, Is.EqualTo(1));
                Assert.That(runner.hasTasks, Is.False);
                Assert.That(pool.count, Is.EqualTo(1),
                    "runner cleanup must return a break-completed block to the pool");

                //the recycled block must be the very same instance, with a still-alive state machine
                var (data2, block2) = pool.Get();
                Assert.That(block2, Is.SameAs(block1));
                Assert.That(data2, Is.SameAs(data1));

                data2.value = 0;
                block2.RunOn(runner);
                runner.WaitForTasksDone(16, 2000);

                Assert.That(data2.value, Is.EqualTo(1), "the recycled state machine must run a full second cycle");
                Assert.That(runner.hasTasks, Is.False);
                Assert.That(pool.count, Is.EqualTo(1));
            }

            pool.Dispose();
        }

        [Test]
        public void Lean_IteratorBlockPool_AbandonedBlockIsNotPooled()
        {
            // What we are testing:
            // A block stopped mid-cycle (before reaching its Break.It boundary) is suspended at an unknown
            // yield: resuming it would continue the previous borrower's operation. The runner must therefore
            // dispose it permanently instead of pooling it.

            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                while (true)
                {
                    data.value++;
                    yield return TaskContract.Yield.It; //stoppable point, NOT a cycle boundary
                    yield return TaskContract.Break.It;
                }
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "AbandonedPool");

            var (data, block) = pool.Get();
            data.value = 0;

            using (var runner = new SteppableRunner("AbandonedPoolRunner"))
            {
                block.RunOn(runner);

                runner.Step(); //value = 1, now suspended at Yield.It
                Assert.That(data.value, Is.EqualTo(1));

                runner.Stop(); //abandons the task mid-cycle
                runner.Step(); //stopping pass: completes and disposes the task

                Assert.That(runner.hasTasks, Is.False);
            }

            Assert.That(data.value, Is.EqualTo(1));
            Assert.That(pool.count, Is.EqualTo(0),
                "a block stopped mid-cycle must not be pooled: it would resume inside the previous cycle");

            pool.Dispose();
        }

        [Test]
        public void Lean_IteratorBlockPool_NaturalCompletionIsNotPooled()
        {
            // What we are testing:
            // A machine that ends naturally (MoveNext() == false, like yield break) is dead: it must be
            // disposed and left to the GC, never pooled.

            IEnumerator<TaskContract> MyIterator(PoolData data)
            {
                data.value++;
                yield return TaskContract.Yield.It;
                data.value++;
                //iterator ends naturally: no Break.It, no enclosing while(true)
            }

            var pool = new IteratorBlockPool<PoolData>(MyIterator, "DeadMachinePool");

            var (data, block) = pool.Get();
            data.value = 0;

            using (var runner = new SteppableRunner("DeadMachinePoolRunner"))
            {
                block.RunOn(runner);
                runner.WaitForTasksDone(16, 2000);

                Assert.That(data.value, Is.EqualTo(2));
            }

            Assert.That(pool.count, Is.EqualTo(0),
                "a naturally completed machine is dead and must never be pooled");

            pool.Dispose();
        }

        [Test]
        public void ExtraLean_IteratorBlockPool_AllowsConcurrentBorrowAndReturn()
        {
            System.Collections.IEnumerator MyIterator(PoolData data)
            {
                while (true)
                    yield return TaskContract.Break.It;
            }

            var pool = new Svelto.Tasks.ExtraLean.IteratorBlockPool<PoolData>(MyIterator, "ConcurrentExtraLeanPool");

            Parallel.For(0, 10_000, _ =>
            {
                var (_, block) = pool.Get();
                block.MoveNext();
            });

            pool.Dispose();
        }

        [Test]
        public void ExtraLean_IteratorBlockPool_NaturalCompletionIsNotPooled()
        {
            // What we are testing:
            // Same dead-machine rule as the Lean pool: a plain IEnumerator that ends naturally must not be
            // returned to the pool, otherwise the next borrower would get a block whose MoveNext is stuck
            // on false forever.

            System.Collections.IEnumerator MyIterator(PoolData data)
            {
                data.value++;
                yield return null;
                data.value++;
                //iterator ends naturally: no Break.It, no enclosing while(true)
            }

            var pool = new Svelto.Tasks.ExtraLean.IteratorBlockPool<PoolData>(MyIterator, "ExtraLeanDeadPool");

            var (data, block) = pool.Get();
            data.value = 0;

            block.MoveNext();
            block.MoveNext(); //returns false: the machine is finished

            Assert.That(data.value, Is.EqualTo(2));

            //the dead block was not pooled: the next Get() must allocate a fresh block and data
            var (data2, block2) = pool.Get();
            Assert.That(block2, Is.Not.SameAs(block));
            Assert.That(data2, Is.Not.SameAs(data));

            pool.Dispose();
        }
    }
}

