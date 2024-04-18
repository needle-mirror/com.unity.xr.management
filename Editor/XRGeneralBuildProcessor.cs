using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

[assembly: InternalsVisibleTo("Unity.XR.Management.EditorTests")]
namespace UnityEditor.XR.Management
{
    /// <summary>
    /// Small utility class for reading, updating and writing boot config.
    /// </summary>
    internal class BootConfig
    {
        public static readonly string kXrBootSettingsKey = "xr-boot-settings";
        Dictionary<string, string> bootConfigSettings;

        BuildTarget m_target;
        string bootConfigPath;

        internal BootConfig(BuildTarget target)
        {
            m_target = target;
        }

        internal void ReadBootConfig()
        {
            bootConfigSettings = new Dictionary<string, string>();

            string buildTargetName = BuildPipeline.GetBuildTargetName(m_target);
            string xrBootSettings = UnityEditor.EditorUserBuildSettings.GetPlatformSettings(buildTargetName, kXrBootSettingsKey);
            if (!String.IsNullOrEmpty(xrBootSettings))
            {
                // boot settings string format
                // <boot setting>:<value>[;<boot setting>:<value>]*
                var bootSettings = xrBootSettings.Split(';');
                foreach (var bootSetting in bootSettings)
                {
                    var setting = bootSetting.Split(':');
                    if (setting.Length == 2 && !String.IsNullOrEmpty(setting[0]) && !String.IsNullOrEmpty(setting[1]))
                    {
                        bootConfigSettings.Add(setting[0], setting[1]);
                    }
                }
            }

        }

        internal void SetValueForKey(string key, string value, bool replace = false)
        {
            if (bootConfigSettings.ContainsKey(key))
            {
                bootConfigSettings[key] = value;
            }
            else
            {
                bootConfigSettings.Add(key, value);
            }
        }

        internal bool DeleteKey(string key)
        {
            return bootConfigSettings.Remove(key);
        }

        internal void WriteBootConfig()
        {
            // boot settings string format
            // <boot setting>:<value>[;<boot setting>:<value>]*
            bool firstEntry = true;
            var sb = new System.Text.StringBuilder();
            foreach (var kvp in bootConfigSettings)
            {
                if (!firstEntry)
                {
                    sb.Append(";");
                }
                sb.Append($"{kvp.Key}:{kvp.Value}");
                firstEntry = false;
            }

            string buildTargetName = BuildPipeline.GetBuildTargetName(m_target);
            EditorUserBuildSettings.SetPlatformSettings(buildTargetName, kXrBootSettingsKey, sb.ToString());
        }
    }

    class XRGeneralBuildProcessor : IPreprocessBuildWithReport
    {
        public static readonly string kPreInitLibraryKey = "xrsdk-pre-init-library";

        class PreInitInfo
        {
            public PreInitInfo(IXRLoaderPreInit loader, BuildTarget buildTarget, BuildTargetGroup buildTargetGroup)
            {
                this.loader = loader;
                this.buildTarget = buildTarget;
                this.buildTargetGroup = buildTargetGroup;
            }

            public IXRLoaderPreInit loader;
            public BuildTarget buildTarget;
            public BuildTargetGroup buildTargetGroup;
        }

        internal static readonly int s_CallbackOrder = 0;
        public int callbackOrder
        {
            get { return s_CallbackOrder; }
        }

        // This has to be static as the OnPostprocessBuild and OnPostGenerateGradleAndroidProject
        // callbacks are made on different instances.
        internal static string s_ManifestPath;
        void CleanOldSettings()
        {
            BuildHelpers.CleanOldSettings<XRGeneralSettings>();
        }

        internal static bool TryGetSettingsPerBuildTarget(out XRGeneralSettingsPerBuildTarget buildTargetSettings)
        {
            // Fix for [1378643](https://fogbugz.unity3d.com/f/cases/1378643/)
            // Ensure that if a settings asset exists in the project, it gets processed.
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out buildTargetSettings))
            {
                if (XRGeneralSettingsPerBuildTarget.TryFindSettingsAsset(out buildTargetSettings))
                {
                    // Asset found but not set. Set the configuration object. If it's empty it will get culled.
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
                }
                else
                {
                    // If no asset is found the processor should not run
                    return false;
                }
            }

            return true;
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            OnPreprocessBuildImpl(report.summary.guid, report.summary.platformGroup, report.summary.platform);
        }

        internal void OnPreprocessBuildImpl(in GUID buildGuid, in BuildTargetGroup targetGroup, in BuildTarget target)
        {
            // Always remember to cleanup preloaded assets after build to make sure we don't
            // dirty later builds with assets that may not be needed or are out of date.
            CleanOldSettings();

  // Fix for [1378643](https://fogbugz.unity3d.com/f/cases/1378643/)
            if (!TryGetSettingsPerBuildTarget(out var buildTargetSettings))
                return;

            XRGeneralSettings settings = buildTargetSettings.SettingsForBuildTarget(targetGroup);
            if (settings == null)
                return;

            XRManagerSettings loaderManager = settings.AssignedSettings;

            if (loaderManager != null)
            {
                var loaders = loaderManager.activeLoaders;

                XRManagementAnalytics.SendBuildEvent(buildGuid, target, targetGroup, loaders);

                // chances are that our devices won't fall back to graphics device types later in the list so it's better to assume the device will be created with the first gfx api in the list.
                // furthermore, we have no way to influence falling back to other graphics API types unless we automatically change settings underneath the user which is no good!
                GraphicsDeviceType[] deviceTypes = PlayerSettings.GetGraphicsAPIs(target);
                if (deviceTypes.Length > 0)
                {
                    VerifyGraphicsAPICompatibility(loaderManager, deviceTypes[0]);
                }
                else
                {
                    Debug.LogWarning("No Graphics APIs have been configured in Player Settings.");
                }

                PreInitInfo preInitInfo = null;
                if (loaders.Count >= 1)
                {
                    preInitInfo = new PreInitInfo(loaders[0] as IXRLoaderPreInit, target, targetGroup);
                }

                var loader = preInitInfo?.loader ?? null;
                BootConfig bootConfig = new BootConfig(target);
                bootConfig.ReadBootConfig();
                if (loader != null)
                {
                    string preInitLibraryName = loader.GetPreInitLibraryName(preInitInfo.buildTarget, preInitInfo.buildTargetGroup);
                    bootConfig.SetValueForKey(kPreInitLibraryKey, preInitLibraryName);
                }
                else
                {
                    bootConfig.DeleteKey(kPreInitLibraryKey);
                }
                bootConfig.WriteBootConfig();
            }

            UnityEngine.Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
            var settingsIncludedInPreloadedAssets = preloadedAssets.Contains(settings);

            // If there are no loaders present in the current manager instance, then the settings will not be included in the current build.
            if (!settingsIncludedInPreloadedAssets && loaderManager.activeLoaders.Count > 0)
            {
                var assets = preloadedAssets.ToList();
                assets.Add(settings);
                PlayerSettings.SetPreloadedAssets(assets.ToArray());
            }
            else
            {
                CleanOldSettings();
            }
        }

        public static void VerifyGraphicsAPICompatibility(XRManagerSettings loaderManager, GraphicsDeviceType selectedDeviceType)
        {
            HashSet<GraphicsDeviceType> allLoaderGraphicsDeviceTypes = new HashSet<GraphicsDeviceType>();
            foreach (var loader in loaderManager.activeLoaders)
            {
                List<GraphicsDeviceType> supporteDeviceTypes = loader.GetSupportedGraphicsDeviceTypes(true);
                // To help with backward compatibility, if we find that any of the compatibility lists are empty we assume that at least one of the loaders does not implement the GetSupportedGraphicsDeviceTypes method
                // Therefore we revert to the previous behavior of building the app regardless of gfx api settings.
                if (supporteDeviceTypes.Count == 0)
                {
                    allLoaderGraphicsDeviceTypes.Clear();
                    break;
                }
                foreach (var supportedGraphicsDeviceType in supporteDeviceTypes)
                {
                    allLoaderGraphicsDeviceTypes.Add(supportedGraphicsDeviceType);
                }
            }


            if (allLoaderGraphicsDeviceTypes.Count > 0 && !allLoaderGraphicsDeviceTypes.Contains(selectedDeviceType))
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendFormat(
                        "The selected graphics API, {0}, is not supported by any of the current loaders. Please change the preferred Graphics API setting in Player Settings.\n",
                        selectedDeviceType);

                foreach (var loader in loaderManager.activeLoaders)
                {
                    stringBuilder.AppendLine(loader.name + " supports:");
                    foreach (var supportedGraphicsDeviceType in loader.GetSupportedGraphicsDeviceTypes(true))
                    {
                        stringBuilder.AppendLine("\t -" + supportedGraphicsDeviceType);
                    }
                }
                throw new BuildFailedException(stringBuilder.ToString());
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            // Always remember to cleanup preloaded assets after build to make sure we don't
            // dirty later builds with assets that may not be needed or are out of date.
            CleanOldSettings();
            
            // This is done to clean up XR-provider-specific manifest entries from Android builds when
            // the incremental build system fails to rebuild the manifest, as there is currently
            // (2023-09-27) no way to mark the manifest as "dirty."
            CleanupAndroidManifest();
        }

        public void CleanupAndroidManifest()
        {
            if (!string.IsNullOrEmpty(s_ManifestPath))
            {
                try
                {
                    File.Delete(s_ManifestPath);
                }
                catch (Exception e)
                {
                    // This only fails if the file can't be deleted; it is quiet if the file does not exist.
                    Debug.LogWarning("Failed to clean up AndroidManifest file located at " + s_ManifestPath + " : " + e.ToString());
                }
            }
            s_ManifestPath = "";
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            const string k_ManifestPath = "/src/main/AndroidManifest.xml";
            s_ManifestPath = path + k_ManifestPath;
        }
    }
}

