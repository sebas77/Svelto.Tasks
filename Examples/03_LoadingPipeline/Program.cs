#pragma warning disable CA1822, CA1852, IDE0060
using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

namespace LoadingPipeline
{
    static class Program
    {
        const int LivePanelRow = 18;
        const int CompletionRow = 34;

        static void SafeClear() { try { Console.Clear(); } catch (System.IO.IOException) { } }
        static void SafeSetCursor(int left, int top) { try { Console.SetCursorPosition(left, top); } catch (System.IO.IOException) { } }
        static void SafeCursorVisible(bool visible) { try { Console.CursorVisible = visible; } catch (System.IO.IOException) { } }

        sealed class GameConfig
        {
            public string world;
            public int maxPlayers;
            public int difficulty;
            public string[] assets;
        }

        static void Main()
        {
            try { Console.Title = "Svelto.Tasks - 03 Loading Pipeline"; } catch (System.IO.IOException) { }
            SafeCursorVisible(false);

            PrintBanner();

            int downloadProgress = 0;
            string status = "idle";

            using (var runner = new SteppableRunner("LoadingRunner"))
            {
                IEnumerator<TaskContract> DownloadAndParse()
                {
                    status = "downloading";
                    for (int i = 0; i < 5; i++)
                    {
                        downloadProgress = (i + 1) * 20;
                        DrawProgress(downloadProgress, status);
                        yield return TaskContract.Yield.It;
                    }
                    status = "parsing";
                    DrawProgress(100, status);
                    yield return TaskContract.Yield.It;

                    var cfg = new GameConfig
                    {
                        world = "Eldoria",
                        maxPlayers = 64,
                        difficulty = 3,
                        assets = new[] { "terrain.hgt", "npcs.spr", "audio.bnk" }
                    };
                    yield return TaskContract.FromReference(cfg);
                }

                IEnumerator<TaskContract> LoadingPipelineTask()
                {
                    status = "starting child";
                    DrawProgress(0, status);

                    var child = DownloadAndParse();
                    yield return child.Continue(); // parent waits for child to finish

                    var cfg = child.Current.ToRef<GameConfig>(); // parent reads the GameConfig returned by child
                    status = "done";
                    DrawResult(cfg);
                }

                LoadingPipelineTask().RunOn(runner);

                while (runner.hasTasks)
                {
                    runner.Step();
                    Thread.Sleep(250);
                }
            }

            SafeSetCursor(0, CompletionRow);
            Console.WriteLine("  ┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  ✅ Pipeline complete! Child returned a GameConfig to parent. │");
            Console.WriteLine("  │  💡 FromReference(cfg) → parent reads child.Current.ToRef<T>() │");
            Console.WriteLine("  └──────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
        }

        static void DrawProgress(int pct, string status)
        {
            int barLen = 20;
            int filled = pct * barLen / 100;
            string bar = new string('█', filled) + new string('░', barLen - filled);

            SafeSetCursor(0, LivePanelRow);
            Console.WriteLine("  ╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  📦 LOADING PIPELINE — child downloads + parses, parent reads  ║");
            Console.WriteLine("  ╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine($"  ║   {status,-12}  [{bar}]  {pct,3}%                ║");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine("  ╚═══════════════════════════════════════════════════════════════╝");
        }

        static void DrawResult(GameConfig cfg)
        {
            SafeSetCursor(0, LivePanelRow);
            Console.WriteLine("  ╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  📦 LOADING PIPELINE — PARSED CONFIG RECEIVED BY PARENT        ║");
            Console.WriteLine("  ╠═══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine("  ║   ┌─────────────────────────────────────────────────────┐     ║");
            Console.WriteLine($"  ║   │  🌍 World:       {cfg.world,-34}│     ║");
            Console.WriteLine($"  ║   │  👥 MaxPlayers:  {cfg.maxPlayers,-34}│     ║");
            Console.WriteLine($"  ║   │  ⚔ Difficulty:   {cfg.difficulty,-34}│     ║");
            Console.WriteLine($"  ║   │  📦 Assets:      {cfg.assets[0],-34}│     ║");
            Console.WriteLine($"  ║   │                 {cfg.assets[1],-34}│     ║");
            Console.WriteLine($"  ║   │                 {cfg.assets[2],-34}│     ║");
            Console.WriteLine("  ║   └─────────────────────────────────────────────────────┘     ║");
            Console.WriteLine("  ║                                                               ║");
            Console.WriteLine("  ╚═══════════════════════════════════════════════════════════════╝");
        }

        static void PrintBanner()
        {
            SafeClear();
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║  03 · LOADING PIPELINE  ·  TaskContract return values       ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  A child task downloads + parses a config, then returns it   ║");
            Console.WriteLine("  ║  to the parent via `yield return                             ║");
            Console.WriteLine("  ║  TaskContract.FromReference(cfg)`. Parent reads it with      ║");
            Console.WriteLine("  ║  child.Current.ToRef<GameConfig>().                          ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("   PARENT ──▶ child = DownloadAndParse()");
            Console.WriteLine("        └─▶ yield child.Continue()   (parent waits)");
            Console.WriteLine("                 CHILD: download ████████░░░  parse");
            Console.WriteLine("                 CHILD: yield TaskContract.FromReference(cfg)");
            Console.WriteLine("        ┌─▶ parent resumes, reads child.Current.ToRef<T>()");
            Console.WriteLine("        ▼");
            Console.WriteLine("   PARENT ◀── cfg received ✅");
            Console.WriteLine();
        }
    }
}
