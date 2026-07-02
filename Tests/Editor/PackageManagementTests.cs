using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management.Metadata;
using Unity.XR.Management.TestPackage;
using Unity.XR.Management.TestPackage.Editor;

namespace UnityEditor.XR.Management.Tests
{
    class PackageManagementTests
    {
        internal static readonly string[] s_TempSettingsPath = { "Temp", "Test" };

        XRGeneralSettingsPerBuildTarget m_TestSettingsPerBuildTarget;
        XRGeneralSettings m_TestSettings;
        XRManagerSettings m_Settings;

        internal static T GetInstanceOfTypeFromAssetDatabase<T>() where T : class
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (assets.Any())
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
                var asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T));
                return asset as T;
            }
            return null;
        }

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset("Assets/XR");

            AssetDatabase.CreateFolder("Assets", "XR");

            m_Settings = ScriptableObject.CreateInstance<XRManagerSettings>();
            m_Settings.name = "Actual testable settings.";

            m_TestSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
            m_TestSettings.Manager = m_Settings;
            m_TestSettings.name = "Standalone Settings Container.";

            m_TestSettingsPerBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            m_TestSettingsPerBuildTarget.SetSettingsForBuildTarget(BuildTargetGroup.Standalone, m_TestSettings);

            var testPath = XRGeneralSettingsTests.GetAssetPathForComponents(s_TempSettingsPath);
            if (!string.IsNullOrEmpty(testPath))
            {
                AssetDatabase.CreateAsset(m_TestSettingsPerBuildTarget, Path.Combine(testPath, "Test_XRGeneralSettings.asset"));

                AssetDatabase.AddObjectToAsset(m_TestSettings, AssetDatabase.GetAssetOrScenePath(m_TestSettingsPerBuildTarget));

                AssetDatabase.CreateFolder(testPath, "Settings");
                testPath = Path.Combine(testPath, "Settings");
                AssetDatabase.CreateAsset(m_Settings, Path.Combine(testPath, "Test_XRSettingsManager.asset"));

                m_TestSettings.Manager = m_Settings;
                AssetDatabase.SaveAssets();
            }

            EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, m_TestSettingsPerBuildTarget, true);

            XRPackageInitializationBootstrap.BeginPackageInitialization();

            TestPackage pkg = new TestPackage();
            XRPackageMetadataStore.AddPluginPackage(pkg);
            XRPackageInitializationBootstrap.InitPackage(pkg);

            TestLoaderBase.WasAssigned = false;
            TestLoaderBase.WasUnassigned = false;
        }

        [TearDown]
        public void Teardown()
        {
            AssetDatabase.DeleteAsset("Assets/Temp");
            AssetDatabase.DeleteAsset("Assets/XR");
        }

        static string LoaderTypeNameForBuildTarget(BuildTargetGroup buildTargetGroup)
        {
            var loaders = XRPackageMetadataStore.GetLoadersForBuildTarget(buildTargetGroup);
            var filteredLoaders = from l in loaders where String.Compare(l.loaderType, typeof(TestLoaderOne).FullName) == 0 select l;

            if (filteredLoaders.Any())
            {
                var loaderInfo = filteredLoaders.First();
                return loaderInfo.loaderType;
            }

            return "";
        }

        bool AssignLoaderToSettings(string loaderTypeName, BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone)
        {
            if (String.IsNullOrEmpty(loaderTypeName))
                return false;

            return XRPackageMetadataStore.AssignLoader(m_Settings, loaderTypeName, buildTargetGroup);
        }

        bool SettingsHasLoaderOfType(string loaderTypeName)
        {
            bool wasFound = false;
            foreach (var l in m_Settings.activeLoaders)
            {
                if (String.Compare(l.GetType().FullName, loaderTypeName) == 0)
                    wasFound = true;
            }
            return wasFound;
        }

        [UnityTest]
        public IEnumerator TestLoaderAssignment()
        {
            Assert.IsNotNull(m_Settings);

            string loaderTypeName = LoaderTypeNameForBuildTarget(BuildTargetGroup.Standalone);
            Assert.IsFalse(String.IsNullOrEmpty(loaderTypeName));

            bool wasFound = false;
            foreach (var l in m_Settings.activeLoaders)
            {
                if (String.Compare(l.GetType().FullName, loaderTypeName) == 0)
                    wasFound = true;
            }
            Assert.IsFalse(wasFound);

            Assert.IsTrue(XRPackageMetadataStore.AssignLoader(m_Settings, loaderTypeName, BuildTargetGroup.Standalone));

            yield return null;

            Assert.IsTrue(SettingsHasLoaderOfType(loaderTypeName));
            Assert.IsTrue(TestLoaderBase.WasAssigned);

        }

        [Test]
        public void TestLoaderAssignmentSerializes()
        {
            Assert.IsNotNull(m_Settings);
            string loaderTypeName = LoaderTypeNameForBuildTarget(BuildTargetGroup.Standalone);
            Assert.IsFalse(String.IsNullOrEmpty(loaderTypeName));
            AssignLoaderToSettings(loaderTypeName);
            Assert.IsTrue(SettingsHasLoaderOfType(loaderTypeName));

            m_Settings = null;
            var settings = GetInstanceOfTypeFromAssetDatabase<XRManagerSettings>();
            m_Settings =  settings;
            Assert.IsNotNull(m_Settings);

            Assert.IsTrue(SettingsHasLoaderOfType(loaderTypeName));
            Assert.IsTrue(TestLoaderBase.WasAssigned);
        }

        [Test]
        public void TestLoaderRemoval()
        {
            Assert.IsNotNull(m_Settings);
            string loaderTypeName = LoaderTypeNameForBuildTarget(BuildTargetGroup.Standalone);
            Assert.IsFalse(String.IsNullOrEmpty(loaderTypeName));
            AssignLoaderToSettings(loaderTypeName);
            Assert.IsTrue(SettingsHasLoaderOfType(loaderTypeName));

            Assert.IsTrue(XRPackageMetadataStore.RemoveLoader(m_Settings, loaderTypeName, BuildTargetGroup.Standalone));

            m_Settings = null;
            var settings = GetInstanceOfTypeFromAssetDatabase<XRManagerSettings>();
            m_Settings = settings;
            Assert.IsNotNull(m_Settings);
            Assert.IsFalse(SettingsHasLoaderOfType(loaderTypeName));

            Assert.IsTrue(TestLoaderBase.WasUnassigned);
        }

        [UnityTest]
        public IEnumerator TestInvalidPackageErrorsOut()
        {
            yield return null;
        }

        [UnityTest]
        public IEnumerator TestNoPackageIdErrorsOut()
        {
            yield return null;
        }
    }
}
