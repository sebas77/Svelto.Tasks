#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace Svelto.Context
{
    public abstract class UnityContext : MonoBehaviour
    {
    }

//a Unity context is a platform specific context wrapper. With Unity, creates a GameObject and add this Monobehaviour
//to it. The GameObject will become one composition root holder. OnContextCreated is called during the Awake of this MB
//OnContextInitialized is called one frame after the MB started, OnContextDestroyed is called when the MB is destroyed
    public class UnityContext<T> : UnityContext where T : class, ICompositionRoot, new()
    {
        void Awake()
        {
            _applicationRoot = new T();

            _applicationRoot.OnContextCreated(this);
        }
        
        void OnDestroy()
        {
            _applicationRoot?.OnContextDestroyed(_hasBeenInitialised);
        }

        void Start()
        {
            if (Application.isPlaying == true)
                StartCoroutine(WaitForFrameworkInitialization());
        }

        IEnumerator WaitForFrameworkInitialization()
        {
            //let's wait until the end of the frame, so we are sure that all the awake and starts are called
            yield return new WaitForEndOfFrame();

            _hasBeenInitialised = true;

            _applicationRoot.OnContextInitialized(this);
        }

        T    _applicationRoot;
        bool _hasBeenInitialised;
    }
}
#endif