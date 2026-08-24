namespace Svelto.Utilities
{
    public static class Utils
    {
        public static int NextPowerOfTwo(int x)
        {
            var result = 2;
            while (result < x)
            {
                result <<= 1;
            }

            return result;
        }
        
        public static uint NextPowerOfTwo(uint x)
        {
            uint result = 2;
            while (result < x)
            {
                result <<= 1;
            }

            return result;
        }
    }
}