namespace Svelto.Utilities
{
        public delegate void ActionRef<T, W>(ref T target, ref W value);
        public delegate void ActionRef<T>(ref    T target);
        public delegate void ActionIn<T>(in    T target);
        
        public delegate W FuncRef<T, W>(ref T target);
        public delegate W FuncIn<T, W>(in T target);
}