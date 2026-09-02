using System;
using System.Collections;
using Svelto.Tasks.ExtraLean;

namespace TestNuget
{
    static class Program
    {
        static void Main()
        {
            int steps = 0;

            IEnumerator CountThree()
            {
                for (int i = 1; i <= 3; i++)
                {
                    steps = i;
                    yield return null;
                }
            }

            using (var runner = new SteppableRunner("TestRunner"))
            {
                CountThree().RunOn(runner);

                while (runner.hasTasks)
                    runner.Step();
            }

            Console.WriteLine($"Svelto.Tasks executed {steps} steps successfully.");
            if (steps != 3)
                throw new Exception("unexpected step count");
        }
    }
}