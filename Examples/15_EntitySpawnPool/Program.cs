using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable CS0436

namespace Example15_EntitySpawnPool
{
    class EntityData
    {
        public int EntityId;
        public int Step;
        public int TotalSteps;
        public string Status;
    }

    static class Program
    {
        static IteratorBlockPool<EntityData> _pool;
        static readonly char[] _spin = { '|', '/', '-', '\\' };
        static int _poolAvailable = 0;
        static int _poolInUse = 0;
        static int _totalSpawned = 0;

        static void SafeClear() { try { Console.Clear(); } catch { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch { } }
        static void SafeTitle(string title) { try { Console.Title = title; } catch { } }
        static void SafeReadKey() { try { Console.ReadKey(); } catch { } }

        static void Main()
        {
            SafeTitle("Svelto.Tasks — IteratorBlockPool Entity Spawn");
            SafeCursorVisible(false);

            PrintBanner();

            _pool = new IteratorBlockPool<EntityData>(EntityLifecycle, "EntitySpawnPool");

            var activeEntities = new List<(EntityData data, PooledIteratorBlock<EntityData> block)>();

            int frame = 0;
            int maxFrames = 30;

            while (frame < maxFrames || activeEntities.Count > 0)
            {
                if (frame < maxFrames && frame % 3 == 0)
                {
                    var (data, block) = _pool.Get();
                    data.EntityId = ++_totalSpawned;
                    data.Step = 0;
                    data.TotalSteps = 4 + (frame % 3);
                    data.Status = "SPAWNING";
                    activeEntities.Add((data, block));
                    _poolAvailable = _pool.count;
                    _poolInUse = activeEntities.Count;
                    DrawScene(frame, activeEntities, "SPAWN E" + data.EntityId);
                }
                else
                {
                    DrawScene(frame, activeEntities, null);
                }

                var stillActive = new List<(EntityData, PooledIteratorBlock<EntityData>)>();
                foreach (var (data, block) in activeEntities)
                {
                    bool alive = block.MoveNext();
                    if (alive)
                    {
                        data.Step++;
                        data.Status = "ACTIVE";
                        stillActive.Add((data, block));
                    }
                    else
                    {
                        //MoveNext returned false because the block yielded Break.It and
                        //recycled itself back into the pool
                    }
                }

                if (stillActive.Count < activeEntities.Count)
                {
                    _poolAvailable = _pool.count;
                    _poolInUse = stillActive.Count;
                    DrawScene(frame, stillActive, "RECYCLE " + (activeEntities.Count - stillActive.Count) + " entity(ies)");
                    Thread.Sleep(300);
                }

                activeEntities = stillActive;
                _poolAvailable = _pool.count;
                _poolInUse = activeEntities.Count;

                frame++;
                Thread.Sleep(200);
            }

            ShowReuseLog();

            _pool.Dispose();

            Console.WriteLine();
            Console.WriteLine("  ✅ Pool disposed. Press any key to exit.");
            SafeCursorVisible(true);
            SafeReadKey();
        }

        static IEnumerator<TaskContract> EntityLifecycle(EntityData data)
        {
            while (true)
            {
                data.Step++;
                yield return TaskContract.Yield.It;

                if (data.Step >= data.TotalSteps)
                    yield return TaskContract.Break.It; //the pool wrapper recycles the block on Break.It
            }
        }

        static void DrawScene(int frame, List<(EntityData data, PooledIteratorBlock<EntityData> block)> active, string action)
        {
            SafeSetCursor(0, 9);
            string spinner = _spin[frame % 4].ToString();

            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  ⚡ Entity Spawn Pool  {0}  Frame {1,3}  {2,2} active    │  ",
                spinner, frame, active.Count);
            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            Console.Write("  │  ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("POOL: {0} available", _poolAvailable);
            Console.ResetColor();
            Console.Write(" │ ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("{0} in use", _poolInUse);
            Console.ResetColor();
            Console.WriteLine("  Total spawned: {0,3}      │  ", _totalSpawned);

            int poolBars = Math.Min(_poolAvailable, 20);
            int useBars = Math.Min(_poolInUse, 20);
            Console.Write("  │  Avail: [");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write(new string('█', poolBars));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', 20 - poolBars));
            Console.ResetColor();
            Console.Write("] InUse: [");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(new string('█', useBars));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(new string('░', 20 - useBars));
            Console.ResetColor();
            Console.WriteLine("]     │  ");

            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");
            Console.WriteLine("  │  Active Entities:                                        │  ");

            int shown = 0;
            foreach (var (data, block) in active)
            {
                if (shown >= 6)
                {
                    Console.WriteLine("  │  ... and {0} more                                        │  ",
                        active.Count - 6);
                    break;
                }

                int lifeBars = Math.Min(data.Step * 20 / Math.Max(1, data.TotalSteps), 20);
                Console.Write("  │    ⚡ E{0:D3} [{1}] {2}/{3} {4,-10}",
                    data.EntityId,
                    new string('█', lifeBars) + new string('░', 20 - lifeBars),
                    data.Step, data.TotalSteps, data.Status);
                Console.WriteLine("       │  ");
                shown++;
            }

            for (int i = shown; i < 6; i++)
                Console.WriteLine("  │    (empty slot)                                          │  ");

            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            if (action != null)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  │  🔄 ACTION: {0,-48}│  ", action);
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("  │  (stepping active entities...)                           │  ");
            }

            Console.WriteLine("  └──────────────────────────────────────────────────────────┘  ");
        }

        static void ShowReuseLog()
        {
            SafeSetCursor(0, 9);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────┐  ");
            Console.WriteLine("  │  🔄 Pool Reuse Verification                               │  ");
            Console.WriteLine("  ├──────────────────────────────────────────────────────────┤  ");

            var (data1, block1) = _pool.Get();
            data1.EntityId = 999;
            data1.Step = 0;
            data1.TotalSteps = 1;
            data1.Status = "TEST";

            //the first MoveNext runs the body up to the first Yield.It, the second one hits
            //Break.It: the wrapper detects the break, recycles the block into the pool and
            //reports the enumerator as finished
            block1.MoveNext();
            bool stillAlive = block1.MoveNext();

            var (data2, block2) = _pool.Get();

            bool sameData = ReferenceEquals(data1, data2);
            bool sameBlock = ReferenceEquals(block1, block2);

            Console.WriteLine("  │  Get() → Entity E999, 2 steps → Break.It → recycled      │  ");
            Console.WriteLine("  │  block finished? {0}                                     │  ",
                stillAlive ? "NO ❌" : "YES ✅");
            Console.WriteLine("  │  Get() again: same Data object?  {0} ({1})           │  ",
                sameData ? "YES ✅" : "NO  ❌",
                sameData ? "REUSED" : "NEW ALLOC");
            Console.WriteLine("  │  Get() again: same Block object? {0} ({1})           │  ",
                sameBlock ? "YES ✅" : "NO  ❌",
                sameBlock ? "REUSED" : "NEW ALLOC");
            Console.WriteLine("  │                                                           │  ");
            Console.WriteLine("  │  💡 Break.It keeps the state machine alive for reuse.     │  ");
            Console.WriteLine("  │     yield break would destroy it — pool would NOT recycle.│  ");
            Console.WriteLine("  └──────────────────────────────────────────────────────────┘  ");

            data2.EntityId = 0;
            data2.Step = 0;
            _pool.Return(data2, block2);
        }

        static void PrintBanner()
        {
            Console.WriteLine();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║   ⚡ Svelto.Tasks Example 15 — Entity Spawn Pool           ║");
            Console.WriteLine("  ║   IteratorBlockPool + Break.It recycling pattern           ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  ⚡ = active entity   🔄 = recycled via Break.It");
            Console.WriteLine();
        }
    }
}