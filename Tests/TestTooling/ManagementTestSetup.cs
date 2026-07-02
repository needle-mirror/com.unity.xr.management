using System;
using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;

namespace Unity.XR.TestTooling
{
    // Mostly borrowed from XRManagement - this should probably live in that package.
    public abstract class ManagementTestSetup
    {
        protected static readonly string[] s_TestGeneralSettings = { "Temp", "Test" };
        protected static readonly string[] s_TempSettingsPath = {"Temp", "Test", "Settings" };

        /// <summary>
        /// When true, AssetDatabase.AddObjectToAsset will not be called to add XRManagerSettings to XRGeneralSettings.
        /// </summary>
        protected virtual bool TestManagerUpgradePath => false;

        protected string testPathToGeneralSettings;
        protected string testPathToSettings;

        UnityEngine.Object m_CurrentSettings;

        protected XRManagerSettings testManager;
        protected XRGeneralSettings xrGeneralSettings;
        protected XRGeneralSettingsPerBuildTarget buildTargetSettings;

        public virtual void SetupTest()
        {
            testManager = ScriptableObject.CreateInstance<XRManagerSettings>();

            xrGeneralSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            xrGeneralSettings.Manager = testManager;

            testPathToSettings = GetAssetPathForComponents(s_TempSettingsPath);
            testPathToSettings = Path.Combine(testPathToSettings, "Test_XRGeneralSettings.asset");
            if (!string.IsNullOrEmpty(testPathToSettings))
            {
                AssetDatabase.CreateAsset(xrGeneralSettings, testPathToSettings);

                if (!TestManagerUpgradePath)
                {
                    AssetDatabase.AddObjectToAsset(testManager, xrGeneralSettings);
                }

                AssetDatabase.SaveAssets();
            }

            testPathToGeneralSettings = GetAssetPathForComponents(s_TestGeneralSettings);
            testPathToGeneralSettings = Path.Combine(testPathToGeneralSettings, "Test_XRGeneralSettingsPerBuildTarget.asset");

            buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            buildTargetSettings.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, xrGeneralSettings);
            if (!string.IsNullOrEmpty(testPathToSettings))
            {
                AssetDatabase.CreateAsset(buildTargetSettings, testPathToGeneralSettings);
                AssetDatabase.SaveAssets();

                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out m_CurrentSettings);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, buildTargetSettings, true);
            }
        }

        public virtual void TearDownTest()
        {
            EditorBuildSettings.RemoveConfigObject(XRGeneralSettings.settingsKey);

            if (!string.IsNullOrEmpty(testPathToGeneralSettings))
            {
                AssetDatabase.DeleteAsset(testPathToGeneralSettings);
            }

            if (!string.IsNullOrEmpty(testPathToSettings))
            {
                AssetDatabase.DeleteAsset(testPathToSettings);
            }

            xrGeneralSettings.Manager = null;
            UnityEngine.Object.DestroyImmediate(xrGeneralSettings);
            xrGeneralSettings = null;

            UnityEngine.Object.DestroyImmediate(testManager);
            testManager = null;

            UnityEngine.Object.DestroyImmediate(buildTargetSettings);
            buildTargetSettings = null;

            if (m_CurrentSettings != null)
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, m_CurrentSettings, true);
            else
                EditorBuildSettings.RemoveConfigObject(XRGeneralSettings.settingsKey);

            AssetDatabase.DeleteAsset(Path.Combine("Assets","Temp"));
        }

        public static string GetAssetPathForComponents(string[] pathComponents, string root = "Assets")
        {
            if (pathComponents.Length <= 0)
                return null;

            string path = root;
            foreach( var pc in pathComponents)
            {
                string subFolder = Path.Combine(path, pc);
                bool shouldCreate = true;
                foreach (var f in AssetDatabase.GetSubFolders(path))
                {
                    if (String.Compare(Path.GetFullPath(f), Path.GetFullPath(subFolder), true) == 0)
                    {
                        shouldCreate = false;
                        break;
                    }
                }

                if (shouldCreate)
                    AssetDatabase.CreateFolder(path, pc);
                path = subFolder;
            }

            return path;
        }
    }
}
