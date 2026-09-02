using System;
using System.Runtime.CompilerServices;

namespace Svelto.DataStructures
{
    public static class HashHelpers
    {
        public static ulong GetFastModMultiplier(uint divisor) => ulong.MaxValue / (ulong) divisor + 1UL;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint FastMod(uint value, uint divisor, ulong multiplier) => (uint) (((multiplier * (ulong) value >> 32) + 1UL) * (ulong) divisor >> 32);
        
        //why prime numbers: https://stackoverflow.com/questions/4638520/why-net-dictionaries-resize-to-prime-numbers
        static readonly int[] primes = {
            1, 3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591};
        static readonly int[] primesHigh = {
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};

        public static int GetPrime(int min)
        {
            if (min <= primes[^1])
            {
                for (var index = 0; index < primes.Length; index++)
                {
                    var prime = primes[index];
                    if (prime >= min)
                        return prime;
                }
            }
            else
            {
                if (min <= primesHigh[^1]) //pay attention this is < and NOT <=
                    for (var index = 0; index < primesHigh.Length; index++)
                    {
                        var prime = primesHigh[index];
                        if (prime >= min)
                            return prime;
                    }
            }

            throw new ArgumentException($"No prime found for {min}");
        }

        public static int Expand(int oldSize)
        {
            if (oldSize < primes[^1]) //pay attention this is < and NOT <=
            {
                var primesLength = primes.Length;
                for (int i = 0; i < primesLength; i++)
                {
                    int prime = primes[i];
                    if (prime > oldSize) //pay attention this is > and NOT >=
                        return prime;
                }
            }
            else
            {
                if (oldSize < primesHigh[^1]) //pay attention this is < and NOT <=
                {
                    var primesLength = primesHigh.Length;
                    for (int i = 0; i < primesLength; i++)
                    {
                        int prime = primesHigh[i];
                        if (prime > oldSize) //pay attention this is > and NOT >=
                            return prime;
                    }
                }
            }

            //outside of our predefined table, just increase by 50%
            return (int)(oldSize * 1.5f);
        }
    }
}
