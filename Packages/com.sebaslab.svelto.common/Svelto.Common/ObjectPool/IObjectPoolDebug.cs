#if DEBUG && !PROFILE_SVELTO
using System;
using System.Collections.Generic;

namespace Svelto.ObjectPool
{
    public interface IObjectPoolDebug
    {
        int GetNumberOfObjectsCreatedSinceLastTime();
        int GetNumberOfObjectsReusedSinceLastTime();
        int GetNumberOfObjectsRecycledSinceLastTime();

        List<ObjectPoolDebugStructure>    DebugPoolInfo(List<ObjectPoolDebugStructure>         debugInfo);
    }

    [Serializable]
    public struct ObjectPoolDebugStructure
    {
        public int key;
        public int count;

        public ObjectPoolDebugStructure(int key, int count) : this()
        {
            this.key   = key;
            this.count = count;
        }
    }
}
#endif