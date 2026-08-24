using System;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks;
using Svelto.Tasks.Lean;

#pragma warning disable

class Program
{
    class TreeNode
    {
        public string Name;
        public List<TreeNode> Children = new List<TreeNode>();
        public int Depth;
    }

    static TreeNode BuildTree()
    {
        var root = new TreeNode { Name = "ROOT", Depth = 0 };
        var a = new TreeNode { Name = "A", Depth = 1 };
        var b = new TreeNode { Name = "B", Depth = 1 };
        var a1 = new TreeNode { Name = "A1", Depth = 2 };
        var a2 = new TreeNode { Name = "A2", Depth = 2 };
        var b1 = new TreeNode { Name = "B1", Depth = 2 };
        var b2 = new TreeNode { Name = "B2", Depth = 2 };
        root.Children.Add(a);
        root.Children.Add(b);
        a.Children.Add(a1);
        a.Children.Add(a2);
        b.Children.Add(b1);
        b.Children.Add(b2);
        return root;
    }

    static void DrawTree(string activeNode)
    {
        string rootC = activeNode == "ROOT" ? "[ROOT]◆" : "[ROOT]";
        string aC = activeNode == "A" ? "[A]◆" : "[A]";
        string bC = activeNode == "B" ? "[B]◆" : "[B]";
        string a1 = activeNode == "A1" ? "[A1]◆" : "[A1]";
        string a2 = activeNode == "A2" ? "[A2]◆" : "[A2]";
        string b1 = activeNode == "B1" ? "[B1]◆" : "[B1]";
        string b2 = activeNode == "B2" ? "[B2]◆" : "[B2]";

        Console.WriteLine("              {0}", rootC);
        Console.WriteLine("             /      \\");
        Console.WriteLine("         {0}        {1}", aC, bC);
        Console.WriteLine("         / \\        / \\");
        Console.WriteLine("       {0} {1}  {2} {3}", a1, a2, b1, b2);
    }

    static void PrintTrace(int depth, string text)
    {
        string indent = new string(' ', depth * 2);
        string arrow = depth == 0 ? "▶" : "→";
        Console.WriteLine("  {0} {1}{2}", arrow, indent, text);
    }

    static string _activeNode;

    static IEnumerator<TaskContract> Traverse(TreeNode node)
    {
        _activeNode = node.Name;
        Console.WriteLine();
        Console.WriteLine("  ┌─ Tree (active: {0}) ──────────────────────", _activeNode);
        DrawTree(_activeNode);
        Console.WriteLine("  └────────────────────────────────────────────");
        PrintTrace(node.Depth, $"ENTERING {node.Name}");
        Thread.Sleep(300);

        yield return TaskContract.Yield.It;

        foreach (var child in node.Children)
        {
            yield return Traverse(child).Continue();

            _activeNode = node.Name;
            PrintTrace(node.Depth, $"back in {node.Name}");
            Thread.Sleep(150);
        }

        PrintTrace(node.Depth, $"EXITING {node.Name}");
        Thread.Sleep(200);
    }

    static void Main()
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch {}

        Console.WriteLine("┌─────────────────────────────────────────────────────────────┐");
        Console.WriteLine("│             RECURSIVE TREE TRAVERSAL                       │");
        Console.WriteLine("│         Deep Continuation Chains via .Continue()            │");
        Console.WriteLine("└─────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Scenario: Walk a scene-graph tree using recursive .Continue()");
        Console.WriteLine("  calls. Each child visit is a continuation the parent waits for.");
        Console.WriteLine("  Depth-first traversal: enter → recurse children → exit.");
        Console.WriteLine();
        Console.WriteLine("  ┌─ The Tree ─────────────────────────────────────────────────┐");
        Console.WriteLine("  │              [ROOT]                                        │");
        Console.WriteLine("  │             /      \\                                       │");
        Console.WriteLine("  │         [A]        [B]                                     │");
        Console.WriteLine("  │         / \\        / \\                                     │");
        Console.WriteLine("  │       [A1][A2]  [B1][B2]                                   │");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Traversal trace (depth-first, via .Continue() recursion):");
        Console.WriteLine("  ────────────────────────────────────────────────────────────");

        var tree = BuildTree();
        var runner = new SteppableRunner("TreeRunner");

        var task = Traverse(tree);
        task.RunOn(runner);

        while (runner.hasTasks)
        {
            runner.Step();
            Thread.Sleep(50);
        }

        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════════════════╗");
        Console.WriteLine("  ║  ✅  Traversal complete!                                    ║");
        Console.WriteLine("  ║  Order: ROOT→A→A1→(back A)→A2→(back A)→B→B1→(back B)→B2  ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("  ┌─ How it worked ────────────────────────────────────────────┐");
        Console.WriteLine("  │ yield return Traverse(child).Continue();                   │");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │ .Continue() spawns the child on the SAME runner and the    │");
        Console.WriteLine("  │ parent task suspends until the child completes. This is    │");
        Console.WriteLine("  │ recursive — each level creates a deeper continuation chain.│");
        Console.WriteLine("  │                                                            │");
        Console.WriteLine("  │ The runner's internal list starts at capacity 3. Deeper    │");
        Console.WriteLine("  │ trees trigger automatic list resizes — tested safe to 32+. │");
        Console.WriteLine("  └────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("  Gotcha: Use .Continue() (not .RunOn(runner)) when the child");
        Console.WriteLine("  should run on the same runner as the parent and the parent");
        Console.WriteLine("  must wait. This demo uses the default StandardFlow; with");
        Console.WriteLine("  SerialFlow a root task could not even wait for another root");
        Console.WriteLine("  task — another reason .Continue() is the right tool here.");
        Console.WriteLine();

        runner.Dispose();
    }
}