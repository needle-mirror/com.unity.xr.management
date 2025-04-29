using System;
using UnityEngine.SubsystemsImplementation;

namespace UnityEngine.XR.Management.Tests.Standalone
{
    public class StandaloneSubsystem : SubsystemWithProvider<StandaloneSubsystem, StandaloneSubsystemDescriptor, StandaloneSubsystem.Provider>
    {
        public class Provider : SubsystemProvider<StandaloneSubsystem>
        {
            public event Action startCalled;
            public event Action stopCalled;
            public event Action destroyCalled;

            public override void Start()
            {
                if (startCalled != null)
                    startCalled.Invoke();
            }

            public override void Stop()
            {
                if (stopCalled != null)
                    stopCalled.Invoke();
            }

            public override void Destroy()
            {
                if (destroyCalled != null)
                    destroyCalled.Invoke();
            }
        }
    }

    public class StandaloneSubsystemImpl : StandaloneSubsystem
    {
        public class ProviderImpl : Provider{ }
    }
}
