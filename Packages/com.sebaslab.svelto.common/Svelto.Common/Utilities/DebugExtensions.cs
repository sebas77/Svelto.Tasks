namespace Svelto.Common.Internal
{
    public static class DebugExtensions
    {
        public static string TypeName<T>(this T _)
        {
            return TypeCache<T>.name;
        }
    }
}