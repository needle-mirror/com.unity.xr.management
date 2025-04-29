using System;
using UnityEngine.SubsystemsImplementation;

namespace UnityEngine.XR.Management.Tests.Standalone
{
    public class StandaloneSubsystemParams
    {
        public string id { get; set;}
        public Type subsystemTypeOverride { get; set; }
        public Type providerType { get; set; }
    }

    public class StandaloneSubsystemDescriptor : SubsystemDescriptorWithProvider<StandaloneSubsystem, StandaloneSubsystem.Provider>
    {
        public static void Create(StandaloneSubsystemParams descriptorParams)
        {
            var descriptor = new StandaloneSubsystemDescriptor(descriptorParams);
            SubsystemDescriptorStore.RegisterDescriptor(descriptor);
        }

        StandaloneSubsystemDescriptor(StandaloneSubsystemParams descriptorParams)
        {
            id = descriptorParams.id;
            subsystemTypeOverride = descriptorParams.subsystemTypeOverride;
            providerType = descriptorParams.providerType;
        }
    }
}
