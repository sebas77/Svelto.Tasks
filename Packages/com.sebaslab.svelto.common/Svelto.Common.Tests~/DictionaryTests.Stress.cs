using System;
using NUnit.Framework;
using Svelto.DataStructures;
using Svelto.DataStructures.Native;

namespace Svelto.Common.Tests
{
    public partial class FasterDictionaryTests
    {
        struct Test
        {
            public int i;

            public Test(int i) : this() { this.i = i; }
        }

        [TestCase]
        public void TestFasterDictionary()
        {
            FasterDictionary<int, Test> test           = new FasterDictionary<int, Test>();
            uint                        dictionarysize = 10000;
            int[]                       numbers        = new int[dictionarysize];
            for (int i = 1; i < dictionarysize; i++)
                numbers[i] = numbers[i - 1] + i * HashHelpers.Expand((int) dictionarysize);

            for (int i = 0; i < dictionarysize; i++)
                test[i] = new Test(numbers[i]);

            for (int i = 0; i < dictionarysize; i++)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i += 2)
                if (test.Remove(i) == false)
                    throw new Exception();

            test.Trim();

            for (int i = 0; i < dictionarysize; i++)
                test[i] = new Test(numbers[i]);

            for (int i = 1; i < dictionarysize - 1; i += 2)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i++)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test.Remove(i) == false)
                    throw new Exception();

            test.Trim();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                test[i] = new Test(numbers[i]);

            for (int i = 0; i < dictionarysize; i++)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i++)
                if (test.Remove(i) == false)
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i++)
                if (test.Remove(i) == true)
                    throw new Exception();

            test.Trim();

            test.Clear();
            for (int i = 0; i < dictionarysize; i++)
                test[numbers[i]] = new Test(i);

            for (int i = 0; i < dictionarysize; i++)
            {
                Test JapaneseCalendar = test[numbers[i]];
                if (JapaneseCalendar.i != i)
                    throw new Exception("read back test failed");
            }
        }

        [Test]
        public void TestReadBack()
        {
            FasterDictionary<int, Test> test           = new FasterDictionary<int, Test>();
            uint                        dictionarysize = 10000;
            int[]                       numbers        = new int[dictionarysize];
            for (int i = 1; i < dictionarysize; i++)
                numbers[i] = numbers[i - 1] + i * HashHelpers.Expand((int) dictionarysize);

            for (int i = 0; i < dictionarysize; i++)
                test[numbers[i]] = new Test(i);

            for (int i = 0; i < dictionarysize; i++)
            {
                Test JapaneseCalendar = test[numbers[i]];
                if (JapaneseCalendar.i != i)
                    throw new Exception("read back test failed");
            }
        }

    }

    public partial class SveltoDictionaryTests
    {
        struct Test
        {
            public int i;

            public Test(int i) : this() { this.i = i; }
        }

        [TestCase(100)]
        [TestCase(1000)]
        [TestCase(10000)]
        public void TestSveltoDictionary(int dictionarysize)
        {
            SveltoDictionary<int, Test, NativeStrategy<SveltoDictionaryNode<int>>, NativeStrategy<Test>, NativeStrategy<int>> test =
                new SveltoDictionary<int, Test, NativeStrategy<SveltoDictionaryNode<int>>, NativeStrategy<Test>, NativeStrategy<int>>(1, Allocator.Persistent);

            int[] numbers = new int[dictionarysize];

            for (int i = 1; i < dictionarysize; i++)
                numbers[i] = numbers[i - 1] + i * HashHelpers.Expand((int) dictionarysize);

            for (int i = 0; i < dictionarysize; i++)
                test[i] = new Test(numbers[i]);

            for (int i = 0; i < dictionarysize; i++)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i += 2)
                if (test.Remove(i) == false)
                    throw new Exception();

            test.Clear();

            for (int i = 0; i < dictionarysize; i++)
                test[i] = new Test(numbers[i]);

            for (int i = 1; i < dictionarysize - 1; i += 2)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = 0; i < dictionarysize; i++)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test.Remove(i) == false)
                    throw new Exception();

            test.Clear();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                test[i] = new Test(numbers[i]);

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test[i].i != numbers[i])
                    throw new Exception();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test.Remove(i) == false)
                    throw new Exception();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test.Remove(i) == true)
                    throw new Exception();

            test.Clear();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                test[i] = new Test(numbers[i]);

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test.Remove(i) == false)
                    throw new Exception();

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                test[i] = new Test(numbers[i]);

            for (int i = (int) (dictionarysize - 1); i >= 0; i -= 3)
                if (test[i].i != numbers[i])
                    throw new Exception();

            test.Clear();
            for (int i = 0; i < dictionarysize; i++)
                test[numbers[i]] = new Test(i);

            for (int i = 0; i < dictionarysize; i++)
            {
                Test JapaneseCalendar = test[numbers[i]];
                if (JapaneseCalendar.i != i)
                    throw new Exception("read back test failed");
            }

            test.Clear();

            for (int i = 0; i < dictionarysize; i++)
                test[numbers[i]] = new Test(i);

            for (int i = 0; i < dictionarysize; i++)
            {
                Test JapaneseCalendar = test[numbers[i]];
                if (JapaneseCalendar.i != i)
                    throw new Exception("read back test failed");
            }

            test.Dispose();
        }
    }
}
