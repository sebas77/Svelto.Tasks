namespace Svelto.Common
{
    public static class TypeToString
    {
        public static string Name<T>(T type)
        {
            return new TypeToString<T>().Name();
        }
    }
    
    public struct TypeToString<T>
    {
        static readonly string name = typeof(T).ToString();

        public string Name()
        {
            return name;
        }
    }
}