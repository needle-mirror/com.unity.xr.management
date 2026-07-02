using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.XR.Management;

namespace UnityEditor.XR.Management
{
    /// <summary>
    /// Container class that holds general settings for each build target group installed in Unity.
    /// </summary>
    [InitializeOnLoad]
    public class XRGeneralSettingsPerBuildTarget : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        internal struct BuildTargetSettings
        {
            public BuildTargetGroup buildTarget;
            public XRGeneralSettings settings;
        }

        [SerializeField, HideInInspector, Obsolete("Deprecated in 4.6.0-pre.1. Use m_SettingsPerBuildTarget instead.")]
        List<BuildTargetGroup> Keys = new();

        [SerializeField, HideInInspector, Obsolete("Deprecated in 4.6.0-pre.1. Use m_SettingsPerBuildTarget instead.")]
        List<XRGeneralSettings> Values = new();

        [SerializeField]
        List<BuildTargetSettings> m_SettingsPerBuildTarget = new();

        Dictionary<BuildTargetGroup, XRGeneralSettings> m_Settings = new();

        static XRGeneralSettingsPerBuildTarget()
        {
            EditorApplication.playModeStateChanged -= PlayModeStateChanged;
            EditorApplication.playModeStateChanged += PlayModeStateChanged;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static bool TryFindSettingsAsset(out XRGeneralSettingsPerBuildTarget generalSettings)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out generalSettings);
            if (generalSettings == null)
            {
                var assets = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                if (assets.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                    generalSettings = AssetDatabase.LoadAssetAtPath(path, typeof(XRGeneralSettingsPerBuildTarget)) as XRGeneralSettingsPerBuildTarget;

                    // If we found the settings asset, make sure it gets cached in the EditorBuildSettings, since it wasn't found initially
                    if (generalSettings != null)
                        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, generalSettings, true);
                }
            }
            return generalSettings != null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static XRGeneralSettingsPerBuildTarget CreateAssetSynchronized()
        {
            var generalSettings = CreateInstance(typeof(XRGeneralSettingsPerBuildTarget)) as XRGeneralSettingsPerBuildTarget;
            string assetPath = EditorUtilities.GetAssetPathForComponents(EditorUtilities.k_DefaultGeneralSettingsPath);
            if (!string.IsNullOrEmpty(assetPath))
            {
                assetPath = Path.Combine(assetPath, "XRGeneralSettingsPerBuildTarget.asset");
                AssetDatabase.CreateAsset(generalSettings, assetPath);
                AssetDatabase.SaveAssets();
            }
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, generalSettings, true);
            return generalSettings;
        }

        internal static XRGeneralSettingsPerBuildTarget GetOrCreate()
            => TryFindSettingsAsset(out var generalSettings) ? generalSettings : CreateAssetSynchronized();

        // Simple class to give us updates when the asset database changes.
        class AssetCallbacks : AssetPostprocessor
        {
            static bool s_Upgrade = true;
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                if (s_Upgrade)
                {
                    s_Upgrade = false;
                    BeginUpgradeSettings();
                }
            }

            static void BeginUpgradeSettings()
            {
                string searchText = "t:XRGeneralSettings";
                string[] assets = AssetDatabase.FindAssets(searchText);
                if (assets.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                    XRGeneralSettingsUpgrade.UpgradeSettingsToPerBuildTarget(path);
                }
            }
        }

        void OnEnable()
        {
            foreach (var setting in m_Settings.Values)
            {
                var manager = setting.Manager;
                if (manager == null)
                    continue;

                var filteredLoaders = from ldr in manager.activeLoaders where ldr != null select ldr;
                manager.TrySetLoaders(filteredLoaders.ToList());
            }
            XRGeneralSettings.Instance = XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        }

        static void PlayModeStateChanged(PlayModeStateChange state)
        {
            XRGeneralSettingsPerBuildTarget buildTargetSettings = null;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out buildTargetSettings);
            if (buildTargetSettings == null)
                return;

            var instance = buildTargetSettings.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (instance == null || !instance.InitManagerOnStart)
                return;

            instance.InternalPlayModeStateChanged(state);
        }

        /// <summary>
        /// Query this settings store to see if there are settings for a specific <see cref="BuildTargetGroup"/>.
        /// </summary>
        /// <param name="buildTargetGroup">Build target to check</param>
        /// <returns>True if there are settings, otherwise false.</returns>
        public bool HasSettingsForBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            return SettingsForBuildTarget(buildTargetGroup) != null;
        }

        /// <summary>
        /// Create default settings for a given build target.
        ///
        /// This <b>will overwrite</b> any current settings for that build target.
        /// </summary>
        /// <param name="buildTargetGroup">Build target to create default settings for.</param>
        public void CreateDefaultSettingsForBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            var settings = CreateInstance<XRGeneralSettings>();
            SetSettingsForBuildTarget(buildTargetGroup, settings);
            settings.name = $"{buildTargetGroup.ToString()} Settings";
            AssetDatabase.AddObjectToAsset(settings, AssetDatabase.GetAssetOrScenePath(this));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Set specific settings for a given build target.
        /// </summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        /// <param name="settings">An instance of <see cref="XRGeneralSettings"/> to assign for the given key.</param>
        public void SetSettingsForBuildTarget(BuildTargetGroup targetGroup, XRGeneralSettings settings)
        {
            if (targetGroup == BuildTargetGroup.Standalone)
                XRGeneralSettings.Instance = settings;
            m_Settings[targetGroup] = settings;
            UpdateSerializedSettings(targetGroup, settings);
        }

        void UpdateSerializedSettings(BuildTargetGroup targetGroup, XRGeneralSettings settings)
        {
            var buildTargetSettings = new BuildTargetSettings
            {
                buildTarget = targetGroup,
                settings = settings
            };

            var existingIndex = m_SettingsPerBuildTarget.FindIndex(x => x.buildTarget == targetGroup);

            if (existingIndex >= 0)
                m_SettingsPerBuildTarget[existingIndex] = buildTargetSettings;
            else
                m_SettingsPerBuildTarget.Add(buildTargetSettings);

            EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Get specific settings for a given build target.
        /// </summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        /// <returns>The instance of <see cref="XRGeneralSettings"/> assigned to the key, or null if not.</returns>
        public XRGeneralSettings SettingsForBuildTarget(BuildTargetGroup targetGroup)
        {
            m_Settings.TryGetValue(targetGroup, out var ret);
            return ret;
        }

        /// <summary>
        /// Check if current settings instance has an instance of <see cref="XRManagerSettings"/>.
        /// </summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        /// <returns>True if it exists, false otherwise.</returns>
        public bool HasManagerSettingsForBuildTarget(BuildTargetGroup targetGroup)
        {
            var settings = SettingsForBuildTarget(targetGroup);
            if (settings == null)
                return false;

            return settings.Manager != null;
        }

        /// <summary>
        /// Create a new default instance of <see cref="XRManagerSettings"/> for a build target. Requires
        /// that the there exists a settings instance for the build target. If there isn't, then one is created.
        ///
        /// This <b>will overwrite</b> any current settings for that build target.
        /// </summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        public void CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup targetGroup)
        {
            if (!HasSettingsForBuildTarget(targetGroup))
                CreateDefaultSettingsForBuildTarget(targetGroup);
            var xrManagerSettings = CreateInstance<XRManagerSettings>();
            xrManagerSettings.name = $"{targetGroup.ToString()} Providers";
            SettingsForBuildTarget(targetGroup).Manager = xrManagerSettings;
            AssetDatabase.AddObjectToAsset(xrManagerSettings, AssetDatabase.GetAssetOrScenePath(this));
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Return the current instance of <see cref="XRManagerSettings"/> for a build target.
        /// </summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        /// <returns>The current instance of <see cref="XRManagerSettings"/>.</returns>
        public XRManagerSettings ManagerSettingsForBuildTarget(BuildTargetGroup targetGroup)
        {
            return SettingsForBuildTarget(targetGroup)?.Manager ?? null;
        }

        /// <summary>
        /// Serialization override.
        /// </summary>
        public void OnBeforeSerialize()
        {
        }

        /// <summary>
        /// Serialization override.
        /// </summary>
        public void OnAfterDeserialize()
        {
            m_Settings.Clear();
            MigrateObsoleteSerializedData();
            UpdateRuntimeMapFromSerializedData();
        }

        void MigrateObsoleteSerializedData()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            if (m_SettingsPerBuildTarget.Count > 0 || Keys == null || Values == null || Keys.Count == 0)
                return;

            for (var i = 0; i < Math.Min(Keys.Count, Values.Count); i++)
            {
                m_SettingsPerBuildTarget.Add(new BuildTargetSettings
                {
                    buildTarget = Keys[i],
                    settings = Values[i]
                });
            }

            Keys.Clear();
            Values.Clear();

            EditorApplication.delayCall += () =>
            {
                // Ensure the scriptable object hasn't been destroyed after serialization finishes
                if (this != null)
                    EditorUtility.SetDirty(this);
            };
#pragma warning restore CS0618 // Type or member is obsolete
        }

        void UpdateRuntimeMapFromSerializedData()
        {
            foreach (var item in m_SettingsPerBuildTarget)
            {
                if (item.settings != null)
                    m_Settings[item.buildTarget] = item.settings;
            }
        }

        /// <summary>Given a build target, get the general settings container assigned to it.</summary>
        /// <param name="targetGroup">An enum specifying which platform group this build is for.</param>
        /// <returns>The instance of <see cref="XRGeneralSettings"/> assigned to the key, or null if not.</returns>
        public static XRGeneralSettings XRGeneralSettingsForBuildTarget(BuildTargetGroup targetGroup)
        {
            if (!TryFindSettingsAsset(out var buildTargetSettings))
                return null;

            return buildTargetSettings.SettingsForBuildTarget(targetGroup);
        }
    }
}
