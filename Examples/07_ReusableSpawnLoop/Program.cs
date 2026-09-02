using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace ReusableSpawnLoop
{
    static class Program
    {
        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        sealed class SpawnData
        {
            public int enemyId;
            public string kind;
        }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 07 Reusable Spawn Loop"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            IEnumerator<TaskContract> SpawnIterator(SpawnData data)
            {
                while (true)
                {
                    data.enemyId++;
                    PrintSpawn(data);
                    yield return TaskContract.Yield.It;
                    Animate(data);
                    yield return TaskContract.Break.It;
                }
            }

            var pool = new IteratorBlockPool<SpawnData>(SpawnIterator, "EnemySpawnPool");

            Console.WriteLine("  ╔════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  CYCLE 1: get from pool → run → Break.It       ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════╝");

            var (data1, block1) = pool.Get();
            data1.kind = "Goblin";
            Console.WriteLine($"  ┌─ pool.Get()   → data.kind = '{data1.kind}'");
            RunBlock(block1);
            Console.WriteLine($"  └─ Break.It hit → Dispose() returned the block to pool (state machine kept alive)");

            SpinWait(28, "recycling", "♻");

            Console.WriteLine();
            Console.WriteLine("  ╔════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  CYCLE 2: get from pool AGAIN                  ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════╝");

            var (data2, block2) = pool.Get();
            data2.kind = "Orc";
            int hash2 = block2.GetHashCode();
            bool sameBlock = ReferenceEquals(block1, block2);
            bool sameData = ReferenceEquals(data1, data2);
            Console.WriteLine($"  ┌─ pool.Get()   → data.kind = '{data2.kind}'  block hash = {hash2}");
            Console.WriteLine($"  │  SAME block instance? {(sameBlock ? "✅ YES" : "❌ NO")}   SAME data instance? {(sameData ? "✅ YES" : "❌ NO")}");
            RunBlock(block2);
            Console.WriteLine($"  └─ Break.It hit → Dispose() returned it again (block reused, no new state machine)");

            Console.WriteLine();
            Console.WriteLine("  ┌────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ♻  RECYCLING DIAGRAM                          │");
            Console.WriteLine("  │                                                │");
            Console.WriteLine("  │   pool.Get() ─▶ Spawn ─▶ yield Break.It        │");
            Console.WriteLine("  │       ▲                     │                  │");
            Console.WriteLine("  │       │                     ▼                  │");
            Console.WriteLine("  │       └── Dispose() → pool.Return() ◀┘         │");
            Console.WriteLine("  │  (state machine stays alive and gets reused)   │");
            Console.WriteLine("  └────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("  ✅ Same PooledIteratorBlock instance reused across cycles.");
            Console.WriteLine("  💡 Break.It keeps the while(true) state machine alive; yield break would destroy it.");
            pool.Dispose();
        }

        static void RunBlock(PooledIteratorBlock<SpawnData> block)
        {
            bool more = block.MoveNext();
            Console.WriteLine($"  │  MoveNext() → {more}   (running, then yields)");
            Thread.Sleep(250);
            more = block.MoveNext();
            Console.WriteLine($"  │  MoveNext() → {more}   (Break.It → flagged for release)");
            Thread.Sleep(250);
            block.Dispose(); //a runner calls Dispose automatically; manually it returns the block to the pool
        }

        static void PrintSpawn(SpawnData data)
        {
            Console.WriteLine($"  │  🟢 Enemy #{data.enemyId,2} spawned: {data.kind}    ");
        }

        static void Animate(SpawnData data)
        {
            Console.Write("  │    ");
            var frames = new[] { "▁", "▃", "▅", "▇", "█", "▇", "▅", "▃" };
            for (int i = 0; i < frames.Length; i++)
            {
                Console.Write($"\r  │    {frames[i]} {data.kind} moving...");
                Thread.Sleep(70);
            }
            Console.WriteLine("    💀 defeated              ");
        }

        static void SpinWait(int frames, string label, string icon)
        {
            var spin = new[] { '|', '/', '─', '\\' };
            for (int i = 0; i < frames; i++)
            {
                Console.Write($"\r  {icon} {label}... {spin[i % 4]}    ");
                Thread.Sleep(60);
            }
            Console.WriteLine($"\r  {icon} {label}... ✓             ");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  07 · REUSABLE SPAWN LOOP  ·  Break.It + IteratorBlockPool  ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  An enemy spawner that recycles the same iterator block      ║");
            Console.WriteLine("  ║  via while(true) { ...; yield return TaskContract.Break.It; }║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
        }
    }
}
