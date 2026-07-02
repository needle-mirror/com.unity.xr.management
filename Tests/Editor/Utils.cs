using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management.Tests
{
    static class Utils
    {
        internal static bool TryGetSettingsPerBuildTarget(out XRGeneralSettingsPerBuildTarget buildTargetSettings)
        {
            // Fix for [1378643](https://fogbugz.unity3d.com/f/cases/1378643/)
            // Ensure that if a settings asset exists in the project, it gets processed.
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out buildTargetSettings))
            {
                if (XRGeneralSettingsPerBuildTarget.TryFindSettingsAsset(out buildTargetSettings))
                {
                    // Asset found but not set. Set the configuration object. If it's empty it will get culled.
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, buildTargetSettings, true);
                }
                else
                {
                    // If no asset is found the processor should not run
                    return false;
                }
            }

            return true;
        }
    }
}
