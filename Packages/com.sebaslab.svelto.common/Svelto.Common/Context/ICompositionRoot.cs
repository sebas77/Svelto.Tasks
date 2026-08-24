namespace Svelto.Context
{
    public interface ICompositionRoot
    {
        void OnContextInitialized<T>(T contextHolder);
        void OnContextDestroyed(bool hasBeenInitialised);
        void OnContextCreated<T>(T contextHolder);
    }
}
