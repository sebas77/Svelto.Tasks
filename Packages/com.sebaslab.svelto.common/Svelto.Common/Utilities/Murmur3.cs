using System;
using System.Diagnostics;

namespace Svelto.Utilities
{
    /// <summary>
    /// Murmur hash.
    ///
    /// Creates an evenly distributed uint hash from a byte[].
    /// Very fast and fairly unique
    /// </summary>
    public static class Murmur3
    {
        const uint C1 = 0xcc9e2d51;
        const uint C2 = 0x1b873593;
        const uint VERIFICATION = 0xB0F57EE3;

        public static uint MurmurHash3_x86_32(byte[] data)
        {
            return MurmurHash3_x86_32(data, (uint) data.Length, 0x1337);
        }

        public static uint MurmurHash3_x86_32(byte[] data, uint h1)
        {
            return MurmurHash3_x86_32(data, (uint) data.Length, h1);
        }

        public static bool VerificationTest()
        {
            var key    = new byte[256];
            var hashes = new byte[1024];

            for (uint i = 0; i < 256; i++)
            {
                key[i] = (byte) i;

                uint result = MurmurHash3_x86_32(key, i, 256 - i);

                Buffer.BlockCopy(BitConverter.GetBytes(result), 0, hashes, (int) i * 4, 4);
            }

            // Then hash the result array

            uint finalr = MurmurHash3_x86_32(hashes, 1024, 0);

            //----------

            if (VERIFICATION != finalr)
            {
                return false;
            }

            Debug.WriteLine("works");

            return true;
        }


        static uint MurmurHash3_x86_32(byte[] data, uint length, uint h1)
        {
            uint nblocks = length >> 2;

            //----------
            // body

            int i = 0;

            for (uint j = nblocks; j > 0; --j)
            {
                uint k1l = BitConverter.ToUInt32(data, i);

                k1l *= C1;
                k1l =  rotl32(k1l, 15);
                k1l *= C2;

                h1 ^= k1l;
                h1 =  rotl32(h1, 13);
                h1 =  h1 * 5 + 0xe6546b64;

                i += 4;
            }

            //----------
            // tail

            nblocks <<= 2;

            uint k1 = 0;

            uint tailLength = length & 3;

            if (tailLength == 3)
                k1 ^= (uint) data[2 + nblocks] << 16;
            if (tailLength >= 2)
                k1 ^= (uint) data[1 + nblocks] << 8;
            if (tailLength >= 1)
            {
                k1 ^= data[nblocks];
                k1 *= C1;
                k1 =  rotl32(k1, 15);
                k1 *= C2;
                h1 ^= k1;
            }

            //----------
            // finalization

            h1 ^= length;

            h1 = fmix32(h1);

            return h1;
        }

        static uint fmix32(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return h;
        }

        static uint rotl32(uint x, byte r)
        {
            return (x << r) | (x >> (32 - r));
        }
    }
}
