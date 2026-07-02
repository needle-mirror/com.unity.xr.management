using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management
{
    /// <summary>
    /// Small utility class for reading, updating and writing boot config.
    /// </summary>
    class BootConfig
    {
        public static readonly string kXrBootSettingsKey = "xr-boot-settings";
        Dictionary<string, string> m_BootConfigSettings;

        BuildTarget m_Target;
        string m_BootConfigPath;

        internal BootConfig(BuildTarget target)
        {
            m_Target = target;
        }

        internal void ReadBootConfig()
        {
            m_BootConfigSettings = new Dictionary<string, string>();

            string buildTargetName = BuildPipeline.GetBuildTargetName(m_Target);
            string xrBootSettings = EditorUserBuildSettings.GetPlatformSettings(buildTargetName, kXrBootSettingsKey);
            if (!string.IsNullOrEmpty(xrBootSettings))
            {
                // boot settings string format
                // <boot setting>:<value>[;<boot setting>:<value>]*
                var bootSettings = xrBootSettings.Split(';');
                foreach (var bootSetting in bootSettings)
                {
                    var setting = bootSetting.Split(':');
                    if (setting.Length == 2 && !String.IsNullOrEmpty(setting[0]) && !String.IsNullOrEmpty(setting[1]))
                    {
                        m_BootConfigSettings.Add(setting[0], setting[1]);
                    }
                }
            }
        }

        internal void SetValueForKey(string key, string value, bool replace = false)
        {
            m_BootConfigSettings[key] = value;
        }

        internal bool DeleteKey(string key)
        {
            return m_BootConfigSettings.Remove(key);
        }

        internal void WriteBootConfig()
        {
            // boot settings string format
            // <boot setting>:<value>[;<boot setting>:<value>]*
            bool firstEntry = true;
            var sb = new StringBuilder();
            foreach (var kvp in m_BootConfigSettings)
            {
                if (!firstEntry)
                {
                    sb.Append(";");
                }
                sb.Append($"{kvp.Key}:{kvp.Value}");
                firstEntry = false;
            }

            string buildTargetName = BuildPipeline.GetBuildTargetName(m_Target);
            EditorUserBuildSettings.SetPlatformSettings(buildTargetName, kXrBootSettingsKey, sb.ToString());
        }
    }

    class XRGeneralBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
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

        internal static readonly int k_CallbackOrder = 0;
        public int callbackOrder => k_CallbackOrder;

        static void CleanOldSettings()
        {
            BuildHelpers.CleanOldSettings<XRGeneralSettings>();
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

            var buildTargetSettings = XRGeneralSettingsPerBuildTarget.GetOrCreate();
            if (!buildTargetSettings)
                return;

            var settings = buildTargetSettings.SettingsForBuildTarget(targetGroup);
            if (settings == null)
                return;

            var loaderManager = settings.Manager;

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

            var preloadedAssets = PlayerSettings.GetPreloadedAssets();
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

        public static void VerifyGraphicsAPICompatibility(
            XRManagerSettings loaderManager, GraphicsDeviceType selectedDeviceType)
        {
            var allLoaderGraphicsDeviceTypes = new HashSet<GraphicsDeviceType>();
            foreach (var loader in loaderManager.activeLoaders)
            {
                var supportedDeviceTypes = loader.GetSupportedGraphicsDeviceTypes(true);
                // To help with backward compatibility, if we find that any of the compatibility lists are empty we assume that at least one of the loaders does not implement the GetSupportedGraphicsDeviceTypes method
                // Therefore we revert to the previous behavior of building the app regardless of gfx api settings.
                if (supportedDeviceTypes.Count == 0)
                {
                    allLoaderGraphicsDeviceTypes.Clear();
                    break;
                }
                foreach (var supportedGraphicsDeviceType in supportedDeviceTypes)
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
        }
    }
}
